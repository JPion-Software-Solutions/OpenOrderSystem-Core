namespace OpenOrderSystem.Core.Services.Ordering.Stages;

/// <summary>
/// Outcome of a manual stage action on one order.
/// </summary>
/// <param name="StageId">The stage the order is in after the call (on success).</param>
public record OrderStageResult(OrderStageStatus Status, Guid OrderId, Guid? StageId = null, string? Message = null)
{
    public bool IsSuccess => Status == OrderStageStatus.Success;
}

public enum OrderStageStatus
{
    Success,

    /// <summary>No such order.</summary>
    NotFound,

    /// <summary>Promotion was requested but the order isn't queued.</summary>
    NotQueued,

    /// <summary>Advance was requested but the order is still queued or has no current stage.</summary>
    NotActive,

    /// <summary>The current stage has no next stage.</summary>
    AtFinalStage,

    /// <summary>A database failure, including another action changing the order at the same moment. Retry.</summary>
    StorageError
}
