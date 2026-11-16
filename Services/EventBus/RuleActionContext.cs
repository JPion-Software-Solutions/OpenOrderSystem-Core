using System.Text.Json;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// Everything an action handler needs to run one queued rule action.
/// </summary>
/// <remarks>
/// <para>
/// The action worker builds a context for each action it takes from the action queue. The action row
/// was written by the dispatcher when a rule matched a system event, so by the time a handler sees the
/// context, rule evaluation and target resolution are already done. The handler only builds
/// instructions and hands them off (device actions) or performs one service call (service actions).
/// </para>
/// <para>
/// Execution is at-least-once: the same action can be delivered more than once, with an increasing
/// <see cref="Attempt"/>. Use <see cref="ActionId"/> as the idempotency key for anything the action
/// produces.
/// </para>
/// </remarks>
public sealed record RuleActionContext
{
    /// <summary>
    /// The queued action being executed.
    /// </summary>
    /// <remarks>
    /// Stable across retries. Use it as the idempotency key for anything the action produces, such as
    /// <see cref="Devices.JobQueue.DeviceJobRequest.SourceActionId"/> for a device job or a dedupe key
    /// passed to an external provider.
    /// </remarks>
    public required Guid ActionId { get; init; }

    /// <summary>
    /// Which run of this action this is: 1 on the first run, incremented on each retry.
    /// </summary>
    public required int Attempt { get; init; }

    /// <summary>
    /// The system event that triggered this action.
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// The key of the triggering event, such as <c>order.placed</c>.
    /// </summary>
    public required string EventKey { get; init; }

    /// <summary>
    /// The rule that matched the event and produced this action.
    /// </summary>
    public required Guid RuleId { get; init; }

    /// <summary>
    /// The key the action handler was resolved by, as declared with <see cref="RuleActionAttribute"/>.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// The device a device action targets, already resolved so handlers never look it up.
    /// <see langword="null"/> for service actions.
    /// </summary>
    /// <remarks>
    /// May name a dependent device. The device job queue resolves the principal device at the root of
    /// its tree when the job is claimed.
    /// </remarks>
    public Guid? TargetDeviceId { get; init; }

    /// <summary>
    /// What the action works from: the event that happened, and how the rule wants the action done.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A JSON object with two separate keys:
    /// </para>
    /// <code>
    /// { "event": { ...event payload... }, "settings": { ...rule settings... } }
    /// </code>
    /// <list type="bullet">
    /// <item>
    /// <c>event</c> is what happened: the facts the event carried into the system, such as order IDs,
    /// order data, report data or a customer's email address. Always present. Read it with
    /// <see cref="RuleActionSettings.TryReadEvent{TEvent}"/>.
    /// </item>
    /// <item>
    /// <c>settings</c> is how this rule wants the action done: static configuration written into the
    /// rule, such as a template name or a fixed recipient list. Never holds event data. May be omitted,
    /// which reads as an empty object. Read it with <see cref="RuleActionSettings.TryReadSettings{TSettings}"/>.
    /// </item>
    /// </list>
    /// <para>
    /// The two are kept apart, never merged into one flat object, so an event field can never collide
    /// with or silently override a setting of the same name, and a handler always knows which values the
    /// rule's author chose and which came from the event.
    /// </para>
    /// <para>
    /// The action worker builds this from the action's settings plus the event payload stored in the
    /// outbox. The element must not depend on a <see cref="JsonDocument"/> that may be disposed; clone it
    /// with <see cref="JsonElement.Clone"/> when building the context.
    /// </para>
    /// </remarks>
    public required JsonElement Input { get; init; }
}
