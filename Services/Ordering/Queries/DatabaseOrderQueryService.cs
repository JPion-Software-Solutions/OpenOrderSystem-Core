using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Queries.Dto;
using OpenOrderSystem.Core.Services.Ordering.Queries.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.Queries;

/// <summary>
/// Database implementation of <see cref="IOrderQueryService"/>. Every query is untracked and mapped to DTOs
/// before it is returned.
/// </summary>
/// <remarks>
/// Stage chains are laid out by <see cref="IOrderStageService.GetChainViewAsync"/>, so a board or a shadow
/// stage resolves orders exactly as every other stage-level view does.
/// </remarks>
public class DatabaseOrderQueryService : IOrderQueryService
{
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly IOrderStageService _stages;
    private readonly IConfirmationNumberCodec _codec;
    private readonly IConfigurationStore _config;
    private readonly ITimeNormalizationService _time;

    /// <summary>
    /// Initializes a new instance of <see cref="DatabaseOrderQueryService"/>.
    /// </summary>
    public DatabaseOrderQueryService(
        IDbContextFactory<OosDbContext> contextFactory,
        IOrderStageService stages,
        IConfirmationNumberCodec codec,
        IConfigurationStore config,
        ITimeNormalizationService time)
    {
        _contextFactory = contextFactory;
        _stages = stages;
        _codec = codec;
        _config = config;
        _time = time;
    }

    /// <inheritdoc/>
    public async Task<DateOnly> GetCurrentBusinessDayAsync()
    {
        var settings = await OrderingSettings.LoadAsync(_config, _time);
        return settings.BusinessDayOf(DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public Task<OrderDetail?> GetOrderAsync(Guid orderId) => GetDetailAsync(o => o.Id == orderId);

    /// <inheritdoc/>
    public Task<OrderDetail?> GetOrderByNumberAsync(int orderNumber) => GetDetailAsync(o => o.OrderNumber == orderNumber);

    /// <inheritdoc/>
    public async Task<OrderDetail?> GetOrderByConfirmationNumberAsync(int confirmationNumber)
    {
        int orderNumber;
        try
        {
            orderNumber = await _codec.ToOrderNumberAsync(confirmationNumber);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        return await GetOrderByNumberAsync(orderNumber);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderSummary>> GetQueueAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var entries = await db.OrderQueue.AsNoTracking().AsSplitQuery()
            .Include(q => q.EntryStage)
            .Include(q => q.OrderHeader!).ThenInclude(o => o.CustomerRecord)
            .Include(q => q.OrderHeader!).ThenInclude(o => o.LineItems)
            .ToListAsync();

        return entries
            .Where(q => q.OrderHeader is not null)
            .OrderBy(q => q.ScheduledFor).ThenBy(q => q.OrderHeader!.OrderNumber)
            .Select(q => ToSummary(q.OrderHeader!, ToQueueInfo(q)))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OrderSummary>?> GetOrdersInStageAsync(Guid stageId, OrderStageFilter? filter = null)
    {
        Guid headerId;
        await using (var db = await _contextFactory.CreateDbContextAsync())
        {
            var stage = await db.OrderStages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stageId);
            if (stage is null)
                return null;
            headerId = stage.HeaderId;
        }

        var view = await _stages.GetChainViewAsync(headerId);
        if (view is null)
            return null;

        var orders = await LoadOrdersOnStagesAsync(view.MainStagesCoveredBy(stageId), filter ?? OrderStageFilter.None);
        return Sort(orders).Select(o => ToSummary(o, null)).ToList();
    }

    /// <inheritdoc/>
    public async Task<OrderStageBoard?> GetStageBoardAsync(Guid headerId, OrderStageFilter? filter = null)
    {
        var view = await _stages.GetChainViewAsync(headerId);
        if (view is null)
            return null;

        var orders = await LoadOrdersOnStagesAsync(view.CoveredMainStages, filter ?? OrderStageFilter.None);
        var byStage = Sort(orders)
            .Select(o => (Stage: view.Resolve(o.CurrentStageId), Order: o))
            .Where(x => x.Stage is not null)
            .ToLookup(x => x.Stage!.Id, x => ToSummary(x.Order, null));

        var columns = view.Stages
            .Select(s => new OrderStageColumn(s.Id, s.Name, view.EffectiveFlags(s), byStage[s.Id].ToList()))
            .ToList();

        return new OrderStageBoard(view.Header.Id, view.Header.Name, view.Header.IsShadow, columns);
    }

    /// <summary>
    /// Loads the active orders on the given main-chain stages, with what a summary needs, narrowed by
    /// <paramref name="filter"/> in the database.
    /// </summary>
    private async Task<List<OrderHeader>> LoadOrdersOnStagesAsync(IReadOnlyList<Guid> mainStageIds, OrderStageFilter filter)
    {
        if (mainStageIds.Count == 0)
            return [];

        await using var db = await _contextFactory.CreateDbContextAsync();
        var query = db.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.CurrentStage)
            .Include(o => o.CustomerRecord)
            .Include(o => o.LineItems)
            .Where(o => o.CurrentStageId != null && mainStageIds.Contains(o.CurrentStageId.Value));

        if (filter.BusinessDay is { } day)
        {
            var (first, last) = OrderNumber.RangeFor(day);
            query = filter.BusinessDayLimitsCompletedOnly
                ? query.Where(o => (o.Flags & OrderHeaderFlags.IsFulfilled) == 0
                                   || (o.OrderNumber >= first && o.OrderNumber <= last))
                : query.Where(o => o.OrderNumber >= first && o.OrderNumber <= last);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// Loads one order with everything on it, plus its queue entry and the names of the stages it has been in.
    /// </summary>
    private async Task<OrderDetail?> GetDetailAsync(System.Linq.Expressions.Expression<Func<OrderHeader, bool>> match)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var order = await db.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.CurrentStage)
            .Include(o => o.CustomerRecord)
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(match);
        if (order is null)
            return null;

        var queueEntry = await db.OrderQueue.AsNoTracking()
            .Include(q => q.EntryStage)
            .FirstOrDefaultAsync(q => q.OrderHeaderId == order.Id);

        var historyStageIds = order.StageHistory.Select(h => h.StageId).Distinct().ToList();
        var stageNames = await db.OrderStages.AsNoTracking()
            .Where(s => historyStageIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name);

        var summary = ToSummary(order, queueEntry is null ? null : ToQueueInfo(queueEntry));
        return new OrderDetail
        {
            Id = summary.Id,
            OrderNumber = summary.OrderNumber,
            OrderDate = summary.OrderDate,
            AssignedTimeSlot = summary.AssignedTimeSlot,
            OrderOrigin = summary.OrderOrigin,
            Flags = summary.Flags,
            CustomerName = summary.CustomerName,
            OrderComments = summary.OrderComments,
            CurrentStageId = summary.CurrentStageId,
            CurrentStageName = summary.CurrentStageName,
            StageEnteredAt = summary.StageEnteredAt,
            StageAdvanceTime = summary.StageAdvanceTime,
            ItemCount = summary.ItemCount,
            Subtotal = summary.Subtotal,
            Queue = summary.Queue,
            ConfirmationNumber = await _codec.ToConfirmationNumberAsync(order.OrderNumber),
            Customer = order.CustomerRecord is { } c ? new OrderCustomer(c.Id, c.Name, c.Phone, c.Email) : null,
            Lines = order.LineItems
                .OrderBy(l => l.ProductName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => l.VariantName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => l.Id)
                .Select(ToLineView)
                .ToList(),
            StageHistory = order.StageHistory
                .OrderBy(h => h.EnteredAt)
                .Select(h => new OrderStageVisit(h.StageId, stageNames.GetValueOrDefault(h.StageId, string.Empty), h.EnteredAt, h.ExitedAt))
                .ToList(),
            Metadata = new Dictionary<string, System.Text.Json.JsonElement>(order.Metadata)
        };
    }

    private static IEnumerable<OrderHeader> Sort(IEnumerable<OrderHeader> orders) =>
        orders.OrderBy(o => o.AssignedTimeSlot).ThenBy(o => o.OrderNumber);

    private static OrderSummary ToSummary(OrderHeader order, OrderQueueInfo? queue) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        OrderDate = order.OrderDate,
        AssignedTimeSlot = order.AssignedTimeSlot,
        OrderOrigin = order.OrderOrigin,
        Flags = order.Flags,
        CustomerName = order.CustomerRecord?.Name,
        OrderComments = order.OrderComments,
        CurrentStageId = order.CurrentStageId,
        CurrentStageName = order.CurrentStage?.Name,
        StageEnteredAt = order.StageHistory.Where(h => h.ExitedAt is null).Select(h => (DateTimeOffset?)h.EnteredAt).Max(),
        StageAdvanceTime = order.StageAdvanceTime,
        ItemCount = order.LineItems.Sum(l => l.Quantity),
        Subtotal = order.LineItems.Sum(l => l.UnitPrice * l.Quantity),
        Queue = queue
    };

    private static OrderQueueInfo ToQueueInfo(OrderQueue entry) =>
        new(entry.ScheduledFor, entry.EntryStageId, entry.EntryStage?.Name ?? string.Empty, entry.CreatedUtc);

    private static OrderLineView ToLineView(OrderLine line) => new(
        line.Id,
        line.ProductId,
        line.ProductName,
        line.VariantName,
        line.UnitPrice,
        line.Quantity,
        line.UnitPrice * line.Quantity,
        line.LineComments,
        line.SelectedOptions);
}
