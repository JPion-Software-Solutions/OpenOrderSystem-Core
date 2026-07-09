namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a single pickup time window that can be claimed by at most one order.
/// </summary>
public class TimeSlot
{
    public int Id { get; set; }

    /// <summary>The scheduled pickup time for this slot.</summary>
    public DateTimeOffset PickupTime { get; set; }

    /// <summary>The order that has claimed this slot, if any.</summary>
    public Guid? OrderId { get; set; }

    /// <inheritdoc cref="OrderId"/>
    public OrderHeader? Order { get; set; }

    /// <summary>
    /// State flags for this slot. Check <see cref="TimeSlotFlags.Blackout"/> to determine
    /// whether the slot is available; combine with <see cref="TimeSlotFlags.HeldInCart"/> or
    /// <see cref="TimeSlotFlags.StaffOverflow"/> to determine why.
    /// </summary>
    public TimeSlotFlags Flags { get; set; } = TimeSlotFlags.None;
}

/// <summary>
/// Bitfield describing the availability state of a <see cref="TimeSlot"/>.
/// <see cref="Blackout"/> is the consumer-facing signal; the remaining flags encode the reason.
/// Stored as a single integer column so new boolean states can be added without a schema change.
/// </summary>
[Flags]
public enum TimeSlotFlags
{
    None = 0,

    /// <summary>Slot is unavailable and cannot be claimed by a new order.</summary>
    Blackout = 1 << 0,

    /// <summary>
    /// A customer has this slot reserved in their cart but has not yet completed checkout.
    /// Always set together with <see cref="Blackout"/>.
    /// </summary>
    HeldInCart = 1 << 1,

    /// <summary>
    /// Staff have blocked this slot to protect preparation time for a large order in an
    /// adjacent slot. Always set together with <see cref="Blackout"/>.
    /// </summary>
    StaffOverflow = 1 << 2
}