namespace OpenOrderSystem.Core.Services.Ordering.Placement.Interfaces;

/// <summary>
/// Scoped service that turns a cart into an order. The only writer of new <c>OrderHeader</c>,
/// <c>OrderLine</c>, <c>CustomerRecord</c> and <c>OrderQueue</c> rows.
/// </summary>
public interface IOrderPlacementService
{
    /// <summary>
    /// Places the cart's draft as a queued order and closes the cart.
    /// </summary>
    /// <remarks>
    /// <para>Requires an active cart with customer details (at least a name) and one or more lines, ordering to be
    /// open, and a slot hold for the cart (<c>ITimekeepingService.HoldSlotAsync</c>). Everything is saved in one
    /// transaction; on any failure nothing is written and the slot hold is kept.</para>
    /// <para>On success the order is queued for its slot, the confirmation number is stored on the cart, and the
    /// cart is disposed. Calling again for a submitted cart returns <see cref="OrderPlacementStatus.AlreadyPlaced"/>
    /// with the same numbers.</para>
    /// <para>Prices and names come from the cart as frozen; the catalog is not rechecked. Snapshot IDs are looked up
    /// including archived products, so archiving a product mid-session doesn't block checkout.</para>
    /// </remarks>
    /// <param name="cartId">The cart to submit.</param>
    /// <param name="entryStageId">The stage the order enters when promoted from the queue.</param>
    /// <param name="orderOrigin">The ordering channel, set by the API layer (e.g. from the API key; <c>"legacy"</c> for the legacy shim).</param>
    Task<OrderPlacementResult> PlaceOrderAsync(Guid cartId, Guid entryStageId, string orderOrigin);
}
