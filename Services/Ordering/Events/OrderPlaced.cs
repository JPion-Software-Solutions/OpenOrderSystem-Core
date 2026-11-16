using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Placement;

namespace OpenOrderSystem.Core.Services.Ordering.Events;

/// <summary>
/// A new order was placed, from any channel.
/// </summary>
/// <remarks>
/// <para>
/// Raised by <see cref="OrderPlacementService"/> in the same transaction that saves the order, so it
/// exists exactly when the order does. Rules on it typically print kitchen tickets or email the customer
/// a confirmation.
/// </para>
/// <para>
/// The payload carries the order's Id plus the facts rules route on. Action handlers load anything else,
/// such as the customer or the full lines, from the order.
/// </para>
/// </remarks>
/// <param name="OrderId">The placed order's <see cref="OrderHeader.Id"/>.</param>
/// <param name="OrderNumber">
/// The order's internal <see cref="OrderHeader.OrderNumber"/>. Not the confirmation number given to the
/// customer.
/// </param>
/// <param name="Origin">The channel the order came from, from <see cref="OrderHeader.OrderOrigin"/>, such as <c>web</c>, <c>in-store</c> or <c>phone</c>.</param>
/// <param name="PickupTime">The pickup time slot, from <see cref="OrderHeader.AssignedTimeSlot"/>.</param>
/// <param name="Subtotal">
/// The sum of <see cref="OrderLine.UnitPrice"/> × <see cref="OrderLine.Quantity"/> over the order's lines,
/// before any tax, discount or fee.
/// </param>
/// <param name="ProductIds">
/// Each distinct product on the order, in line order, without lines that have no product. A flat array so
/// a rule can match a product with <c>cont</c>.
/// </param>
[SystemEvent("order.placed",
    DisplayName = "Order Placed",
    Description = "A new order was placed, from any channel. Carries the order's Id and number, its origin, pickup time, subtotal and product Ids.")]
public sealed record OrderPlaced(
    Guid OrderId,
    int OrderNumber,
    string Origin,
    DateTimeOffset PickupTime,
    decimal Subtotal,
    IReadOnlyList<Guid> ProductIds) : ISystemEvent
{
    /// <summary>
    /// Builds the event for <paramref name="order"/>, from its header and its
    /// <see cref="OrderHeader.LineItems"/>.
    /// </summary>
    /// <param name="order">The order being placed, with its lines loaded.</param>
    public static OrderPlaced From(OrderHeader order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new OrderPlaced(
            order.Id,
            order.OrderNumber,
            order.OrderOrigin,
            order.AssignedTimeSlot,
            order.LineItems.Sum(l => l.UnitPrice * l.Quantity),
            order.LineItems
                .Where(l => l.ProductId.HasValue)
                .Select(l => l.ProductId!.Value)
                .Distinct()
                .ToArray());
    }
}
