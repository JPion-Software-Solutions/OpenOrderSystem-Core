namespace OpenOrderSystem.Core.Services.Ordering.Timekeeping.Interfaces;

/// <summary>
/// Singleton service for pickup slots: availability, in-memory holds, ASAP slot selection, and whether
/// ordering is currently open. Owns no database tables; placed orders are counted from <c>OrderHeader</c>.
/// </summary>
/// <remarks>
/// <para>Each cart holds at most one slot. A hold counts against the slot's capacity until it is completed,
/// released, or expires. Placement <see cref="ClaimHold">claims</see> a hold before writing the order, then
/// <see cref="CompleteHold">completes</see> it on success or <see cref="UnclaimHold">unclaims</see> it on
/// failure, so the customer keeps the slot if placement fails and a double-submit can't use one hold twice.</para>
/// <para>Only same-day ordering is supported: holds can only be placed on today's business day.</para>
/// </remarks>
public interface ITimekeepingService
{
    /// <summary>
    /// Whether orders are accepted right now: within today's configured hours, the accepting-orders switch
    /// is on, and a staff terminal is online.
    /// </summary>
    Task<bool> IsAcceptingOrdersAsync();

    /// <summary>
    /// Every slot on a business day with its remaining capacity. Empty if the day is closed or hours aren't configured.
    /// </summary>
    Task<IReadOnlyList<SlotAvailability>> GetSlotsAsync(DateOnly businessDay);

    /// <summary>
    /// Holds a slot for a cart, replacing any hold the cart already has.
    /// </summary>
    /// <param name="cartId">The cart the hold belongs to.</param>
    /// <param name="requested">A slot start from <see cref="GetSlotsAsync"/>, or <see langword="null"/> for ASAP
    /// (the first selectable slot today).</param>
    Task<SlotHoldResult> HoldSlotAsync(Guid cartId, DateTimeOffset? requested);

    /// <summary>The cart's hold, or <see langword="null"/> if it has none or it expired.</summary>
    SlotHold? GetHold(Guid cartId);

    /// <summary>
    /// Marks the cart's hold as in use by a placement. Returns <see langword="null"/> if there is no valid hold or
    /// it is already claimed. A claimed hold doesn't expire and still counts against capacity.
    /// </summary>
    SlotHold? ClaimHold(Guid cartId);

    /// <summary>Removes a claimed hold once its order has been saved.</summary>
    void CompleteHold(Guid cartId);

    /// <summary>Returns a claimed hold to normal after a failed placement, restarting its expiry.</summary>
    void UnclaimHold(Guid cartId);

    /// <summary>Removes the cart's hold (e.g. the customer changed their mind or the cart was abandoned).</summary>
    void ReleaseHold(Guid cartId);

    /// <summary>The configured lead time (z): minimum time between placement and pickup.</summary>
    Task<TimeSpan> GetLeadTimeAsync();
}
