using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers;
using OpenOrderSystem.Core.Services.Ordering.Reporting.Dto;
using OpenOrderSystem.Core.Services.Ordering.Reporting.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.Reporting;

/// <summary>
/// Database implementation of <see cref="IOrderReportingService"/>.
/// </summary>
/// <remarks>
/// The period's orders are found by order number range, which is indexed, and loaded once with their lines.
/// Every section is then worked out in memory. A business day holds at most 999 orders, so this stays cheap.
/// </remarks>
public class DatabaseOrderReportingService : IOrderReportingService
{
    private const OrderHeaderFlags NOT_SALES = OrderHeaderFlags.IsWaste | OrderHeaderFlags.IsComp;

    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly IConfigurationStore _config;
    private readonly ITimeNormalizationService _time;

    /// <summary>
    /// Initializes a new instance of <see cref="DatabaseOrderReportingService"/>.
    /// </summary>
    public DatabaseOrderReportingService(
        IDbContextFactory<OosDbContext> contextFactory,
        IConfigurationStore config,
        ITimeNormalizationService time)
    {
        _contextFactory = contextFactory;
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
    public async Task<OrderReport> GetOrderReportAsync(ReportPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (period.LastDay < period.FirstDay)
            throw new ArgumentException("The period's last day is before its first day.", nameof(period));

        var first = OrderNumber.RangeFor(period.FirstDay).First;
        var last = OrderNumber.RangeFor(period.LastDay).Last;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(await _time.GetLocalTimezoneId());

        List<OrderHeader> orders;
        HashSet<Guid> queued;
        await using (var db = await _contextFactory.CreateDbContextAsync())
        {
            orders = await db.Orders.AsNoTracking()
                .Include(o => o.LineItems)
                .Where(o => o.OrderNumber >= first && o.OrderNumber <= last)
                .ToListAsync();

            var ids = orders.Select(o => o.Id).ToList();
            queued = (await db.OrderQueue.AsNoTracking()
                    .Where(q => ids.Contains(q.OrderHeaderId))
                    .Select(q => q.OrderHeaderId)
                    .ToListAsync())
                .ToHashSet();
        }

        bool IsCanceled(OrderHeader o) =>
            o.Flags.HasFlag(OrderHeaderFlags.IsCanceled) || (o.CurrentStageId is null && !queued.Contains(o.Id));

        var canceled = orders.Where(IsCanceled).ToList();
        var live = orders.Where(o => !IsCanceled(o)).ToList();
        var sales = live.Where(o => (o.Flags & NOT_SALES) == 0).ToList();

        return new OrderReport
        {
            Period = period,
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = new OrderStatusCounts(
                Placed: orders.Count,
                Queued: live.Count(o => queued.Contains(o.Id)),
                InProgress: live.Count(o => !queued.Contains(o.Id) && !o.Flags.HasFlag(OrderHeaderFlags.IsFulfilled)),
                Fulfilled: live.Count(o => !queued.Contains(o.Id) && o.Flags.HasFlag(OrderHeaderFlags.IsFulfilled)),
                Canceled: canceled.Count),
            Sales = Figures(sales),
            Waste = Figures(live.Where(o => o.Flags.HasFlag(OrderHeaderFlags.IsWaste))),
            Comp = Figures(live.Where(o => o.Flags.HasFlag(OrderHeaderFlags.IsComp))),
            Canceled = Figures(canceled),
            ByOrigin = sales
                .GroupBy(o => o.OrderOrigin, StringComparer.OrdinalIgnoreCase)
                .Select(g => new OriginSales(g.Key, Figures(g)))
                .OrderByDescending(o => o.Figures.Subtotal).ThenBy(o => o.Origin, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ByHour = sales
                .GroupBy(o => TimeZoneInfo.ConvertTime(o.OrderDate, timeZone).Hour)
                .Select(g => new HourlySales(g.Key, Figures(g)))
                .OrderBy(h => h.Hour)
                .ToList(),
            Products = ProductSalesOf(sales)
        };
    }

    private static SalesFigures Figures(IEnumerable<OrderHeader> orders)
    {
        var list = orders as IReadOnlyCollection<OrderHeader> ?? orders.ToList();
        var subtotal = list.Sum(Subtotal);
        return new SalesFigures(
            list.Count,
            list.Sum(o => o.LineItems.Sum(l => l.Quantity)),
            subtotal,
            list.Count == 0 ? 0m : Math.Round(subtotal / list.Count, 2, MidpointRounding.AwayFromZero));
    }

    private static decimal Subtotal(OrderHeader order) => order.LineItems.Sum(l => l.UnitPrice * l.Quantity);

    private static List<ProductSales> ProductSalesOf(IEnumerable<OrderHeader> sales) =>
        sales
            .SelectMany(o => o.LineItems.Select(l => (Order: o, Line: l)))
            .GroupBy(x => x.Line.ProductId is { } id ? id.ToString() : "name:" + x.Line.ProductName.ToUpperInvariant())
            .Select(g =>
            {
                var latest = g.MaxBy(x => x.Order.OrderDate).Line;
                return new ProductSales(
                    latest.ProductId,
                    latest.ProductName,
                    g.Sum(x => x.Line.Quantity),
                    g.Sum(x => x.Line.UnitPrice * x.Line.Quantity),
                    g.Select(x => x.Order.Id).Distinct().Count());
            })
            .OrderByDescending(p => p.Quantity).ThenByDescending(p => p.Revenue).ThenBy(p => p.ProductName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
