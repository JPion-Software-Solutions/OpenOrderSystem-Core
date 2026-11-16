namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// Marks a type as a system event: a record of something that happened in OOS, such as an order being
/// placed, that rules can react to.
/// </summary>
/// <remarks>
/// <para>
/// System events are the first step of the event bus pipeline. Code that changes state raises an event
/// with <see cref="ISystemEventBus"/>, which writes it to the outbox in the same transaction as the change.
/// The dispatcher later reads the outbox, matches the event against event rules, and queues their
/// actions.
/// </para>
/// <para>
/// An event describes what happened (<c>order.placed</c>), never what to do about it
/// (<c>print.ticket</c>); deciding that is the rules' job. Keep payloads thin: the IDs of what changed,
/// plus the facts rules route on. Action handlers load anything else they need.
/// </para>
/// <para>
/// Every event type is an immutable record placed with its domain, implements this interface, and
/// declares its key with <see cref="SystemEventAttribute"/>. It is serialized into the outbox with
/// <see cref="RuleActionSettings.EventOptions"/>, and handlers read it back with
/// <see cref="RuleActionSettings.TryReadEvent{TEvent}"/>. Add fields freely; renaming or removing one
/// breaks rules and handlers that use it.
/// </para>
/// </remarks>
public interface ISystemEvent;
