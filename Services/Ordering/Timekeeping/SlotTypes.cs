namespace OpenOrderSystem.Core.Services.Ordering.Timekeeping;

/// <summary>
/// One pickup slot on a business day.
/// </summary>
/// <param name="Slot">Slot start (UTC).</param>
/// <param name="Capacity">Maximum orders for the slot.</param>
/// <param name="Remaining">Capacity left after placed orders and other carts' active holds.</param>
/// <param name="IsSelectable">Whether a customer can choose it now: capacity remains and it is at least the lead time away.</param>
public record SlotAvailability(DateTimeOffset Slot, int Capacity, int Remaining, bool IsSelectable);

/// <summary>
/// An in-memory reservation of a slot for one cart.
/// </summary>
public record SlotHold(Guid CartId, DateTimeOffset Slot, DateTimeOffset ExpiresAt);

/// <summary>
/// Outcome of <see cref="Interfaces.ITimekeepingService.HoldSlotAsync"/>.
/// </summary>
public record SlotHoldResult(SlotHoldStatus Status, SlotHold? Hold = null, string? Message = null)
{
    public bool IsSuccess => Status == SlotHoldStatus.Held;
}

public enum SlotHoldStatus
{
    /// <summary>The slot is held for the cart; see <see cref="SlotHoldResult.Hold"/>.</summary>
    Held,

    /// <summary>Ordering is closed (outside hours, switched off, or no staff terminal online).</summary>
    NotAcceptingOrders,

    /// <summary>The requested time isn't a slot today, or is within the lead time.</summary>
    InvalidSlot,

    /// <summary>The slot is full, or (for ASAP) no slot is left today.</summary>
    Unavailable
}
