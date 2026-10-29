namespace OpenOrderSystem.Core.Services.Ordering.Placement;

/// <summary>
/// Outcome of <see cref="Interfaces.IOrderPlacementService.PlaceOrderAsync"/>.
/// </summary>
/// <remarks>
/// On <see cref="OrderPlacementStatus.Placed"/> and <see cref="OrderPlacementStatus.AlreadyPlaced"/>,
/// <see cref="OrderNumber"/> and <see cref="ConfirmationNumber"/> are set; give the customer
/// <see cref="ConfirmationNumber"/>. <see cref="Message"/> is customer-presentable on every failure.
/// </remarks>
public record OrderPlacementResult(
    OrderPlacementStatus Status,
    Guid? OrderId = null,
    int? OrderNumber = null,
    int? ConfirmationNumber = null,
    DateTimeOffset? AssignedTimeSlot = null,
    string? Message = null)
{
    public bool IsSuccess => Status is OrderPlacementStatus.Placed or OrderPlacementStatus.AlreadyPlaced;
}

public enum OrderPlacementStatus
{
    /// <summary>The order was created and the cart closed.</summary>
    Placed,

    /// <summary>The cart was already submitted. The existing order's numbers are returned; nothing new was created.</summary>
    AlreadyPlaced,

    /// <summary>No such cart, or it was cancelled.</summary>
    CartNotFound,

    /// <summary>The cart has no customer details, no name, or no lines.</summary>
    CartIncomplete,

    /// <summary>Ordering is closed right now.</summary>
    NotAcceptingOrders,

    /// <summary>The cart has no valid slot hold (never held, expired, or another submission is using it).</summary>
    SlotUnavailable,

    /// <summary>The entry stage doesn't exist.</summary>
    StageNotFound,

    /// <summary>A product in the cart has no snapshot (shouldn't happen; the catalog writes one on create).</summary>
    SnapshotMissing,

    /// <summary>The business day's 999 order numbers are used up.</summary>
    DailyLimitReached,

    /// <summary>A database failure. Nothing was saved and the slot hold is kept so the customer can retry.</summary>
    StorageError
}
