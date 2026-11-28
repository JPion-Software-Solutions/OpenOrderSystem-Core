namespace OpenOrderSystem.Core.Services.Ordering.Reporting.Dto;

/// <summary>
/// Aggregate figures for the orders placed in a <see cref="ReportPeriod"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every order placed in the period has exactly one status in <see cref="Status"/>. "Sales" are the orders that
/// are not canceled, waste or comp: they are what <see cref="Sales"/>, <see cref="ByOrigin"/>,
/// <see cref="ByHour"/> and <see cref="Products"/> are worked out from. Waste and comp orders are counted on
/// their own in <see cref="Waste"/> and <see cref="Comp"/>, because they were made but not paid for.
/// </para>
/// <para>
/// Money is the sum of line totals, options included, before order-level adjustments such as promotions.
/// </para>
/// <para>
/// To report something new, add a section here and build it in the reporting service from the orders it has
/// already loaded.
/// </para>
/// </remarks>
public sealed record OrderReport
{
    /// <summary>The business days covered.</summary>
    public required ReportPeriod Period { get; init; }

    /// <summary>When the report was built. Orders still in progress can change afterwards.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>How many of the period's orders are in each status.</summary>
    public required OrderStatusCounts Status { get; init; }

    /// <summary>Count and value of the sales.</summary>
    public required SalesFigures Sales { get; init; }

    /// <summary>Orders flagged as waste: made, then thrown away.</summary>
    public required SalesFigures Waste { get; init; }

    /// <summary>Orders flagged as comp: given away.</summary>
    public required SalesFigures Comp { get; init; }

    /// <summary>Canceled or abandoned orders, with the value they would have had.</summary>
    public required SalesFigures Canceled { get; init; }

    /// <summary>Sales by order origin, largest subtotal first.</summary>
    public required IReadOnlyList<OriginSales> ByOrigin { get; init; }

    /// <summary>
    /// Sales by the store-local hour the order was placed, for every hour that had any, earliest first. The
    /// hour is that of the placement time, not the pickup time slot.
    /// </summary>
    public required IReadOnlyList<HourlySales> ByHour { get; init; }

    /// <summary>Every product sold, most items sold first. Take the first few for a "top products" list.</summary>
    public required IReadOnlyList<ProductSales> Products { get; init; }
}

/// <summary>
/// The business days a report covers, inclusive. A single day has <see cref="FirstDay"/> equal to
/// <see cref="LastDay"/>.
/// </summary>
/// <remarks>
/// An order belongs to the business day encoded in its order number, the day it was placed.
/// </remarks>
public sealed record ReportPeriod(DateOnly FirstDay, DateOnly LastDay)
{
    /// <summary>A period of one business day.</summary>
    public static ReportPeriod Day(DateOnly day) => new(day, day);

    public static ReportPeriod Week(DateOnly starting) => new(starting, starting.AddDays(7));
    
    public static ReportPeriod Create(DateOnly startDate, DateOnly endDate) => new(startDate, endDate);
}

/// <summary>
/// How many orders are in each status. Every order is in exactly one, so the four add up to <see cref="Placed"/>.
/// </summary>
/// <param name="Placed">Every order placed in the period.</param>
/// <param name="Queued">Waiting in the queue to be promoted into a stage.</param>
/// <param name="InProgress">In a stage, not yet complete.</param>
/// <param name="Fulfilled">Complete: reached a terminal stage.</param>
/// <param name="Canceled">
/// Canceled or abandoned: flagged <c>IsCanceled</c>, or neither queued nor in any stage. Every order is placed
/// into the queue, so one that is in neither has been taken out of the flow.
/// </param>
public sealed record OrderStatusCounts(int Placed, int Queued, int InProgress, int Fulfilled, int Canceled);

/// <summary>
/// The count and value of a set of orders.
/// </summary>
/// <param name="Orders">How many orders.</param>
/// <param name="Items">How many items: the sum of every line's quantity.</param>
/// <param name="Subtotal">The sum of their subtotals.</param>
/// <param name="AverageOrder">The mean subtotal, or zero when there are no orders.</param>
public sealed record SalesFigures(int Orders, int Items, decimal Subtotal, decimal AverageOrder);

/// <summary>Sales from one order origin.</summary>
public sealed record OriginSales(string Origin, SalesFigures Figures);

/// <summary>Sales placed in one store-local hour (0–23) of the period.</summary>
public sealed record HourlySales(int Hour, SalesFigures Figures);

/// <summary>
/// Sales of one product.
/// </summary>
/// <param name="ProductId">The product, or <see langword="null"/> for lines whose product has been deleted, grouped by name.</param>
/// <param name="ProductName">The name on the most recent order line.</param>
/// <param name="Quantity">Items sold.</param>
/// <param name="Revenue">The sum of its line totals.</param>
/// <param name="Orders">How many orders included it.</param>
public sealed record ProductSales(Guid? ProductId, string ProductName, int Quantity, decimal Revenue, int Orders);
