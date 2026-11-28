namespace OpenOrderSystem.Core.Services.Ordering.Queries;

/// <summary>
/// Narrows a stage query to the orders a view needs.
/// </summary>
/// <remarks>
/// Orders stay in a chain's terminal stage after they are complete, so an unfiltered query of a terminal stage
/// returns every completed order ever. A staff terminal typically wants every active order, plus only the
/// orders completed today: set <see cref="BusinessDay"/> to today and
/// <see cref="BusinessDayLimitsCompletedOnly"/> to true.
/// </remarks>
public sealed record OrderStageFilter
{
    /// <summary>No filtering: every order in the stages queried.</summary>
    public static OrderStageFilter None { get; } = new();

    /// <summary>
    /// Only orders placed on this business day, or <see langword="null"/> for any day. The business day comes
    /// from the order number, so this is the day the order was placed, not its time slot or when it was
    /// completed.
    /// </summary>
    public DateOnly? BusinessDay { get; init; }

    /// <summary>
    /// When true, <see cref="BusinessDay"/> only limits completed (fulfilled) orders, and every order still in
    /// progress is returned whatever day it was placed.
    /// </summary>
    public bool BusinessDayLimitsCompletedOnly { get; init; }
}
