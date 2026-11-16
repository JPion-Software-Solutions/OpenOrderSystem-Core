using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;

namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// Raises system events by adding them to the transactional outbox (<see cref="OutboxEvent"/>).
/// </summary>
/// <remarks>
/// <para>
/// This is the first step of the event bus pipeline: code that changes state raises an event describing
/// what happened, and the dispatcher later matches it against event rules.
/// </para>
/// <para>
/// Every method takes the <see cref="OosDbContext"/> to write to and never saves. Pass the same context
/// that holds the state change and save it yourself: the change and its event are then committed in one
/// transaction, or neither is. This works the same for a scoped context and one created from
/// <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/>.
/// </para>
/// <para>
/// Registered as a singleton; it holds no state of its own.
/// </para>
/// </remarks>
public interface ISystemEventBus
{
    /// <summary>
    /// Adds <paramref name="evt"/> to <paramref name="db"/>'s outbox, without saving.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The event is only written when the caller saves <paramref name="db"/>. If the caller never saves,
    /// or the save fails, the event is discarded along with their change.
    /// </para>
    /// <para>
    /// The event's key is looked up by its runtime type, so an event held as <see cref="ISystemEvent"/>
    /// is raised as the record it really is. The row gets a new version 7 GUID as its Id and the current
    /// time as <see cref="OutboxEvent.OccurredAt"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">The event's static type. Only used to accept the argument.</typeparam>
    /// <param name="db">The context holding the state change. The caller saves it.</param>
    /// <param name="evt">What happened.</param>
    /// <returns>The new outbox row's Id, which becomes the event's Id.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="evt"/>'s type is not a registered event (it has no
    /// <see cref="SystemEventAttribute"/>, or was not loaded at startup). This is a programming error, not
    /// bad input.
    /// </exception>
    Guid Raise<TEvent>(OosDbContext db, TEvent evt) where TEvent : ISystemEvent;

    /// <summary>
    /// Adds <paramref name="evt"/> to <paramref name="db"/>'s outbox unless an event with the same key
    /// and <paramref name="dedupeKey"/> was already raised, without saving.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For events that must only ever exist once, such as scheduled events: a Quartz job can fire twice
    /// for the same occurrence. Choose a <paramref name="dedupeKey"/> naming that occurrence, such as the
    /// business date in ISO format.
    /// </para>
    /// <para>
    /// The Id is deterministic: a UUID version 8 (RFC 9562) built from the first 16 bytes of SHA-256 over
    /// the UTF-8 of <c>&lt;eventKey&gt;:&lt;dedupeKey&gt;</c>. The same event key and dedupe key always
    /// give the same Id. A row with that Id already in the database, or already added to
    /// <paramref name="db"/> but not yet saved, means the event was raised, and nothing is added.
    /// </para>
    /// <para>
    /// As with <see cref="Raise{TEvent}"/>, the event is only written when the caller saves
    /// <paramref name="db"/>.
    /// </para>
    /// <para>
    /// The existence check is not race-proof across multiple app instances. OOS runs as a single instance
    /// per store, so this is acceptable. With multiple instances, the primary key still prevents a
    /// duplicate row, at the cost of a failed save.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">The event's static type. Only used to accept the argument.</typeparam>
    /// <param name="db">The context to add the event to. The caller saves it.</param>
    /// <param name="evt">What happened.</param>
    /// <param name="dedupeKey">Identifies this occurrence of the event. Must not be blank.</param>
    /// <param name="ct">Cancels the existence check.</param>
    /// <returns>The event's Id, and whether it was newly raised by this call.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="evt"/>'s type is not a registered event.
    /// </exception>
    Task<RaiseOnceResult> RaiseOnceAsync<TEvent>(OosDbContext db, TEvent evt, string dedupeKey, CancellationToken ct = default)
        where TEvent : ISystemEvent;
}

/// <summary>
/// The result of <see cref="ISystemEventBus.RaiseOnceAsync{TEvent}"/>.
/// </summary>
/// <param name="Id">The event's deterministic Id, whether or not it was newly raised.</param>
/// <param name="Raised">
/// <see langword="true"/> if this call added the event; <see langword="false"/> if it already existed,
/// saved or only added to the context.
/// </param>
public readonly record struct RaiseOnceResult(Guid Id, bool Raised);
