using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a queued order awaiting entry into the active fulfillment workflow.
/// </summary>
/// <remarks>
/// <para>
/// Orders enter the queue at placement time when a future <see cref="Ordering.OrderHeader.AssignedTimeSlot"/>
/// is specified. The queue entry is removed and the associated <see cref="OrderHeader.IsQueued"/> flag
/// is cleared atomically when the order is promoted into the active stage chain, either automatically
/// by the scheduled advancement job when <see cref="ScheduledFor"/> is reached, or manually by staff.
/// </para>
/// <para>
/// Slot availability is determined by querying <see cref="Ordering.OrderHeader.AssignedTimeSlot"/> across
/// all orders where <see cref="OrderHeaderFlags.IsQueued"/> is set on <see cref="OrderHeader.Flags"/>,
/// compared against the configured maximum orders per slot. Slots within the configured minimum lead
/// time window are excluded from customer selection.
/// </para>
/// <para>
/// <see cref="CustomerRecord"/> rows associated with queued orders are exempt from data retention
/// purges for the duration of the order's time in the queue.
/// </para>
/// </remarks>
public class OrderQueue
{
    /// <summary>
    /// Unique identifier for this queue entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the associated <see cref="OrderHeader"/>.
    /// </summary>
    public Guid OrderHeaderId { get; set; }

    /// <summary>
    /// Navigation property for the associated order header.
    /// </summary>
    public OrderHeader? OrderHeader { get; set; }

    /// <summary>
    /// The order's pickup slot, copied from <see cref="Ordering.OrderHeader.AssignedTimeSlot"/>. The order is
    /// promoted once <c>ScheduledFor - leadTime &lt;= now</c>; the lead time is read from configuration at
    /// runtime rather than stored.
    /// </summary>
    public DateTimeOffset ScheduledFor { get; set; }

    /// <summary>
    /// The stage the order enters when promoted. Chosen by the caller at placement because an instance
    /// may run several flows, each with its own initial stage.
    /// </summary>
    public Guid EntryStageId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="EntryStageId"/>.
    /// </summary>
    public OrderStage? EntryStage { get; set; }

    /// <summary>
    /// The date and time this queue entry was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}