using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Services.Ordering.Queries.Dto;

/// <summary>
/// A read-only view of one order, with what a list or a stage board shows. <see cref="OrderDetail"/> adds
/// everything else.
/// </summary>
/// <remarks>
/// A snapshot taken when the query ran. Changing an order goes through the stage and placement services,
/// never through this.
/// </remarks>
public record OrderSummary
{
    /// <summary>The order's ID.</summary>
    public required Guid Id { get; init; }

    /// <summary>The plain order number staff see, in the form <c>xxxxxyyy</c> (business day, then sequence).</summary>
    public required int OrderNumber { get; init; }

    /// <summary>When the order was placed.</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>The pickup time slot the order is assigned to.</summary>
    public required DateTimeOffset AssignedTimeSlot { get; init; }

    /// <summary>Where the order came from, such as <c>online</c> or <c>counter</c>.</summary>
    public required string OrderOrigin { get; init; }

    /// <summary>The order's flags: queued, fulfilled, canceled, waste, comp.</summary>
    public required OrderHeaderFlags Flags { get; init; }

    /// <summary>The customer's name, or <see langword="null"/> when the order has no customer record.</summary>
    public string? CustomerName { get; init; }

    /// <summary>The comments on the order as a whole.</summary>
    public string? OrderComments { get; init; }

    /// <summary>The main-chain stage the order is in, or <see langword="null"/> while it is queued.</summary>
    public Guid? CurrentStageId { get; init; }

    /// <summary>The name of <see cref="CurrentStageId"/>.</summary>
    public string? CurrentStageName { get; init; }

    /// <summary>When the order entered its current stage.</summary>
    public DateTimeOffset? StageEnteredAt { get; init; }

    /// <summary>
    /// When the order moves on from its current stage by itself, or <see langword="null"/> when the stage
    /// needs staff to move it on.
    /// </summary>
    public DateTimeOffset? StageAdvanceTime { get; init; }

    /// <summary>The number of items: the sum of every line's quantity.</summary>
    public required int ItemCount { get; init; }

    /// <summary>
    /// The sum of the line totals. Each line's unit price already includes its options. This is before any
    /// order-level adjustments, such as promotions.
    /// </summary>
    public required decimal Subtotal { get; init; }

    /// <summary>The order's place in the queue, or <see langword="null"/> when it is not queued.</summary>
    public OrderQueueInfo? Queue { get; init; }
}

/// <summary>
/// Where a queued order is waiting to go.
/// </summary>
/// <param name="ScheduledFor">The time slot the order is queued for. It is promoted once this is within the lead time.</param>
/// <param name="EntryStageId">The stage it enters when promoted.</param>
/// <param name="EntryStageName">The name of <paramref name="EntryStageId"/>.</param>
/// <param name="QueuedAt">When it was queued.</param>
public sealed record OrderQueueInfo(
    DateTimeOffset ScheduledFor,
    Guid EntryStageId,
    string EntryStageName,
    DateTimeOffset QueuedAt);
