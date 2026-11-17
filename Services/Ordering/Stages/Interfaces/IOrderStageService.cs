namespace OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;

/// <summary>
/// Scoped service that moves orders from the queue into their stage chain and along it, and lays chains out
/// for stage-level views.
/// </summary>
/// <remarks>
/// <para>Entering a stage closes the open <c>StageHistory</c> entry, appends a new one, and sets
/// <c>StageAdvanceTime</c> from the stage's <c>AutoCompleteTime</c> (minutes), or clears it for a stage that
/// requires manual progression. Entering a stage flagged <c>IsTerminal</c> marks the order <c>IsFulfilled</c>:
/// the order is complete.</para>
/// <para>Orders only ever move along main chains (see <c>OrderStageHeader</c>). Shadow chains are never
/// entered; views resolve an order's place in them from its main-chain stage.</para>
/// <para>Stage entry/exit effects (tickets, sounds, notifications) are not implemented yet. Order-scoped flags
/// belong to this service; view-scoped flags belong to the views.</para>
/// </remarks>
public interface IOrderStageService
{
    /// <summary>
    /// Promotes every queued order whose slot is within the lead time (<c>ScheduledFor - leadTime &lt;= now</c>)
    /// into its entry stage. Called by the order stage job.
    /// </summary>
    /// <returns>The number of orders promoted.</returns>
    Task<int> PromoteDueOrdersAsync();

    /// <summary>
    /// Advances every active order whose <c>StageAdvanceTime</c> has passed to its stage's next stage.
    /// Called by the order stage job.
    /// </summary>
    /// <returns>The number of orders advanced.</returns>
    Task<int> AdvanceDueOrdersAsync();

    /// <summary>Staff early promotion: moves a queued order into its entry stage now.</summary>
    Task<OrderStageResult> PromoteNowAsync(Guid orderId);

    /// <summary>Staff manual advance: moves an active order to its stage's next stage now.</summary>
    Task<OrderStageResult> AdvanceNowAsync(Guid orderId);

    /// <summary>
    /// Loads a stage chain, main or shadow, for a stage-level view.
    /// </summary>
    /// <param name="headerId">The chain's <c>OrderStageHeader</c> id.</param>
    /// <returns>The chain view, or <see langword="null"/> if no such chain exists. Entities are not tracked.</returns>
    Task<OrderStageChainView?> GetChainViewAsync(Guid headerId);
}
