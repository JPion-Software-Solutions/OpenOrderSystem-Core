using OpenOrderSystem.Core.Services.Ordering.Queries.Dto;

namespace OpenOrderSystem.Core.Services.Ordering.Queries.Interfaces;

/// <summary>
/// Scoped, read-only service for finding orders and laying them out: one order, the queue, a stage, or a
/// whole stage chain. It serves live views such as the staff terminal; aggregate figures for reports belong
/// to the reporting service.
/// </summary>
/// <remarks>
/// <para>
/// Every result is a DTO snapshot, never a tracked entity, so nothing read here can change an order. Changes
/// go through the stage and placement services.
/// </para>
/// <para>
/// The service only queries and organizes. How often a view refreshes, and whether it is pushed changes, is up
/// to the UI and API layers.
/// </para>
/// </remarks>
public interface IOrderQueryService
{
    /// <summary>The store's current business day, for building an <see cref="OrderStageFilter"/>.</summary>
    Task<DateOnly> GetCurrentBusinessDayAsync();

    /// <summary>One order, by ID.</summary>
    /// <returns>The order, or <see langword="null"/> if there is none with that ID.</returns>
    Task<OrderDetail?> GetOrderAsync(Guid orderId);

    /// <summary>One order, by its plain order number.</summary>
    /// <returns>The order, or <see langword="null"/> if there is none with that number.</returns>
    Task<OrderDetail?> GetOrderByNumberAsync(int orderNumber);

    /// <summary>One order, by the confirmation number the customer was given.</summary>
    /// <returns>
    /// The order, or <see langword="null"/> if the number is out of range or matches no order.
    /// </returns>
    Task<OrderDetail?> GetOrderByConfirmationNumberAsync(int confirmationNumber);

    /// <summary>
    /// Every queued order, soonest <see cref="OrderQueueInfo.ScheduledFor"/> first. Each has
    /// <see cref="OrderSummary.Queue"/> set.
    /// </summary>
    Task<IReadOnlyList<OrderSummary>> GetQueueAsync();

    /// <summary>
    /// The orders in one stage, by time slot and then order number.
    /// </summary>
    /// <remarks>
    /// The stage can be on a main or a shadow chain. A shadow stage holds the orders on every main-chain stage
    /// it covers.
    /// </remarks>
    /// <param name="stageId">The stage.</param>
    /// <param name="filter">Narrows the orders returned. Defaults to <see cref="OrderStageFilter.None"/>.</param>
    /// <returns>The orders, or <see langword="null"/> if there is no such stage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The filter's business day is outside the order number range.</exception>
    Task<IReadOnlyList<OrderSummary>?> GetOrdersInStageAsync(Guid stageId, OrderStageFilter? filter = null);

    /// <summary>
    /// A whole stage chain, main or shadow, with its orders sorted into its stages.
    /// </summary>
    /// <remarks>
    /// Stages are in chain order. An order that doesn't appear in the chain, for example one before the first
    /// anchor of a shadow chain, is left out.
    /// </remarks>
    /// <param name="headerId">The chain's <c>OrderStageHeader</c> ID.</param>
    /// <param name="filter">Narrows the orders returned. Defaults to <see cref="OrderStageFilter.None"/>.</param>
    /// <returns>The board, or <see langword="null"/> if there is no such chain.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The filter's business day is outside the order number range.</exception>
    Task<OrderStageBoard?> GetStageBoardAsync(Guid headerId, OrderStageFilter? filter = null);
}
