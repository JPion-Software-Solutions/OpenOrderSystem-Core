using System.ComponentModel.DataAnnotations;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Data.DataModels.V2.EventBus;

/// <summary>
/// A system event waiting in, or already handled from, the transactional outbox.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISystemEventBus"/> adds a row to the same <see cref="OosDbContext"/> as the state change
/// that raised the event, so both are committed in one transaction, or neither is. An event is never
/// recorded for a change that rolled back, and a committed change never loses its event.
/// </para>
/// <para>
/// The dispatcher reads unprocessed rows (<see cref="ProcessedAt"/> is <see langword="null"/>), matches
/// each against the <see cref="EventRule"/>s for its <see cref="EventKey"/>, writes the resulting action
/// rows, and sets <see cref="ProcessedAt"/>.
/// </para>
/// <para>
/// Events are processed in <see cref="OccurredAt"/> order, never <see cref="Id"/> order. Ids from
/// <see cref="ISystemEventBus.Raise{TEvent}"/> are version 7 GUIDs, but Ids from
/// <see cref="ISystemEventBus.RaiseOnceAsync{TEvent}"/> are deterministic version 8 GUIDs derived from a
/// hash, which carry no time and sort randomly. SQL Server also does not sort <c>uniqueidentifier</c> in
/// creation order.
/// </para>
/// </remarks>
public class OutboxEvent
{
    /// <summary>
    /// The longest event key the database holds, used by both <see cref="EventKey"/> and
    /// <see cref="EventRule.EventKey"/>. Event keys longer than this fail startup.
    /// </summary>
    public const int EventKeyMaxLength = 100;

    /// <summary>
    /// Unique identifier for the event, passed to each action it produces as
    /// <see cref="RuleActionContext.EventId"/>.
    /// </summary>
    /// <remarks>
    /// A version 7 GUID for events raised with <see cref="ISystemEventBus.Raise{TEvent}"/>, or a
    /// deterministic version 8 GUID for events raised with
    /// <see cref="ISystemEventBus.RaiseOnceAsync{TEvent}"/>, where the primary key is what keeps the event
    /// unique. Not time-ordered; see <see cref="OccurredAt"/>.
    /// </remarks>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The event's key, such as <c>order.placed</c>, as declared by its <see cref="SystemEventAttribute"/>.
    /// The dispatcher matches it against <see cref="EventRule.EventKey"/>.
    /// </summary>
    [MaxLength(EventKeyMaxLength)]
    public string EventKey { get; set; } = string.Empty;

    /// <summary>
    /// The event record serialized as a JSON object with <see cref="RuleActionSettings.EventOptions"/>.
    /// </summary>
    /// <remarks>
    /// Rule triggers are evaluated against it, and it becomes the <c>event</c> part of each action's
    /// <see cref="RuleActionContext.Input"/>, which handlers read with
    /// <see cref="RuleActionSettings.TryReadEvent{TEvent}"/>.
    /// </remarks>
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// When the event was raised. The dispatcher processes events in this order.
    /// </summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// When the dispatcher finished handling the event, or <see langword="null"/> while it is still
    /// waiting.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// How many times the dispatcher has tried to handle the event. Zero until the first try.
    /// </summary>
    /// <remarks>
    /// Together with <see cref="LastError"/>, this makes an event that keeps failing to dispatch visible
    /// rather than silently stuck.
    /// </remarks>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Why the dispatcher's most recent try failed, or <see langword="null"/> if it has not failed.
    /// </summary>
    public string? LastError { get; set; }
}
