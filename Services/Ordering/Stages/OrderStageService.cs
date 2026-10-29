using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.Stages;

/// <summary>
/// Default scoped implementation of <see cref="IOrderStageService"/>.
/// </summary>
public class OrderStageService : IOrderStageService
{
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly ITimekeepingService _timekeeping;
    private readonly ILogger<OrderStageService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OrderStageService"/>.
    /// </summary>
    public OrderStageService(
        IDbContextFactory<OosDbContext> contextFactory,
        ITimekeepingService timekeeping,
        ILogger<OrderStageService> logger)
    {
        _contextFactory = contextFactory;
        _timekeeping = timekeeping;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> PromoteDueOrdersAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now + await _timekeeping.GetLeadTimeAsync();

        await using var db = await _contextFactory.CreateDbContextAsync();
        var due = await db.OrderQueue
            .Include(q => q.OrderHeader)
            .Include(q => q.EntryStage)
            .Where(q => q.ScheduledFor <= cutoff)
            .ToListAsync();
        if (due.Count == 0)
            return 0;

        foreach (var entry in due)
            Promote(db, entry, now);

        return await SaveBatchAsync(db, due.Count, "promote");
    }

    /// <inheritdoc/>
    public async Task<int> AdvanceDueOrdersAsync()
    {
        var now = DateTimeOffset.UtcNow;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var due = await db.Orders
            .Include(o => o.CurrentStage)
            .Where(o => o.CurrentStageId != null && o.StageAdvanceTime != null && o.StageAdvanceTime <= now)
            .ToListAsync();
        if (due.Count == 0)
            return 0;

        var nextStages = await LoadNextStagesAsync(db, due);
        var advanced = 0;
        foreach (var order in due)
        {
            if (order.CurrentStage?.NextStageId is { } nextId && nextStages.TryGetValue(nextId, out var next))
            {
                EnterStage(order, next, now);
                advanced++;
            }
            else
            {
                // Auto-advance time on a stage with nowhere to go: stop retrying it every tick.
                _logger.LogWarning("Order {OrderNumber} is due to advance but stage '{StageId}' has no next stage.",
                    order.OrderNumber, order.CurrentStageId);
                order.StageAdvanceTime = null;
            }
        }

        return await SaveBatchAsync(db, advanced, "advance");
    }

    /// <inheritdoc/>
    public async Task<OrderStageResult> PromoteNowAsync(Guid orderId)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync();
            var entry = await db.OrderQueue
                .Include(q => q.OrderHeader)
                .Include(q => q.EntryStage)
                .FirstOrDefaultAsync(q => q.OrderHeaderId == orderId);
            if (entry is null)
                return await db.Orders.AnyAsync(o => o.Id == orderId)
                    ? new OrderStageResult(OrderStageStatus.NotQueued, orderId, Message: "The order isn't queued.")
                    : new OrderStageResult(OrderStageStatus.NotFound, orderId);

            Promote(db, entry, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            return new OrderStageResult(OrderStageStatus.Success, orderId, entry.EntryStageId);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Early promotion of order '{OrderId}' failed.", orderId);
            return new OrderStageResult(OrderStageStatus.StorageError, orderId, Message: "The order changed while it was being promoted. Please retry.");
        }
    }

    /// <inheritdoc/>
    public async Task<OrderStageResult> AdvanceNowAsync(Guid orderId)
    {
        try
        {
            await using var db = await _contextFactory.CreateDbContextAsync();
            var order = await db.Orders
                .Include(o => o.CurrentStage)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order is null)
                return new OrderStageResult(OrderStageStatus.NotFound, orderId);
            if (order.CurrentStage is null || order.Flags.HasFlag(OrderHeaderFlags.IsQueued))
                return new OrderStageResult(OrderStageStatus.NotActive, orderId, Message: "The order hasn't entered a stage yet.");
            if (order.CurrentStage.NextStageId is not { } nextId)
                return new OrderStageResult(OrderStageStatus.AtFinalStage, orderId, order.CurrentStageId, "The order is already at its final stage.");

            var next = await db.OrderStages.FirstAsync(s => s.Id == nextId);
            EnterStage(order, next, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
            return new OrderStageResult(OrderStageStatus.Success, orderId, next.Id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Manual advance of order '{OrderId}' failed.", orderId);
            return new OrderStageResult(OrderStageStatus.StorageError, orderId, Message: "The order changed while it was being advanced. Please retry.");
        }
    }

    private static void Promote(OosDbContext db, OrderQueue entry, DateTimeOffset now)
    {
        var order = entry.OrderHeader!;
        order.Flags &= ~OrderHeaderFlags.IsQueued;
        EnterStage(order, entry.EntryStage!, now);
        db.OrderQueue.Remove(entry);
    }

    private static void EnterStage(OrderHeader order, OrderStage stage, DateTimeOffset now)
    {
        foreach (var open in order.StageHistory.Where(h => h.ExitedAt is null))
            open.ExitedAt = now;

        order.StageHistory.Add(new OrderStageHistory { StageId = stage.Id, EnteredAt = now });
        order.CurrentStageId = stage.Id;
        order.CurrentStage = stage;

        if (stage.Flags.HasFlag(OrderStageFlags.IsTerminal))
        {
            order.Flags |= OrderHeaderFlags.IsFulfilled;
            order.StageAdvanceTime = null;
        }
        else
        {
            order.StageAdvanceTime = stage.AutoCompleteTime is { } minutes ? now.AddMinutes((double)minutes) : null;
        }
    }

    private static async Task<Dictionary<Guid, OrderStage>> LoadNextStagesAsync(OosDbContext db, IEnumerable<OrderHeader> orders)
    {
        var ids = orders.Select(o => o.CurrentStage?.NextStageId).OfType<Guid>().Distinct().ToList();
        return await db.OrderStages.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
    }

    /// <summary>
    /// Saves a batch from the job. If a staff action changed one of the orders at the same moment, the whole
    /// batch is dropped and picked up again on the next tick.
    /// </summary>
    private async Task<int> SaveBatchAsync(OosDbContext db, int count, string action)
    {
        try
        {
            await db.SaveChangesAsync();
            return count;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogInformation(ex, "Order stage job: {Action} batch conflicted with another change; will retry next run.", action);
            return 0;
        }
    }
}
