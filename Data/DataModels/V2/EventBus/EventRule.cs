using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Data.DataModels.V2.EventBus;

/// <summary>
/// A data-driven rule that decides which actions run when a system event is raised.
/// </summary>
/// <remarks>
/// <para>
/// When an event is dispatched, every rule with a matching <see cref="EventKey"/> that is not flagged
/// <see cref="EventRuleFlags.Disabled"/> is evaluated in <see cref="Priority"/> order. If a rule's trigger
/// matches the event, each action listed in the rule is queued with the rule's settings for it. A matched
/// rule's flow mode (see <see cref="Flags"/>) decides whether evaluation continues to the remaining rules.
/// </para>
/// <para>
/// Rules belong to the event bus, not to any device. A device action names its target device by
/// <see cref="Devices.DeviceHead.Key"/>, which never changes once the device is created, so renaming a
/// device never breaks a rule. Because rules are data, routing can be changed at runtime without a
/// restart; only new action implementations require a deploy.
/// </para>
/// </remarks>
public class EventRule
{
    /// <summary>
    /// Unique identifier for the rule. Generated as a version 7 (time-ordered) GUID.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The key of the system event this rule responds to, such as <c>order.placed</c>.
    /// </summary>
    /// <remarks>
    /// Must be the key of an event in the <see cref="Services.EventBus.Interfaces.ISystemEventCatalog"/>, and
    /// not blank. At most <see cref="OutboxEvent.EventKeyMaxLength"/> characters, like the event keys
    /// themselves.
    /// </remarks>
    [MaxLength(OutboxEvent.EventKeyMaxLength)]
    public string EventKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional human-readable explanation of what the rule does, shown in the admin area.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Flow mode and behavioral flags for this rule.
    /// </summary>
    /// <seealso cref="EventRuleFlags"/>
    public EventRuleFlags Flags { get; set; } = EventRuleFlags.None;

    /// <summary>
    /// Evaluation order among rules for the same event. Higher values are evaluated first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rules with equal priority are evaluated in <see cref="Id"/> order (oldest first), so evaluation is
    /// deterministic.
    /// </para>
    /// <para>
    /// That ordering is done in memory with <see cref="Guid.CompareTo(Guid)"/>, which orders version 7
    /// GUIDs by creation time, not with <c>ORDER BY Id</c>. SQL Server does not sort
    /// <c>uniqueidentifier</c> in creation order, so sorting by <see cref="Id"/> in the database gives a
    /// different order on different providers.
    /// </para>
    /// </remarks>
    public int Priority { get; set; }

    /// <summary>
    /// The schema version of the JSON in <see cref="Rules"/>.
    /// </summary>
    /// <remarks>
    /// Kept as its own column rather than inside the JSON, so the body can be read with the right format
    /// before it is parsed. Incremented when the rule body's format changes, so older rules can be read
    /// correctly or migrated to the current format. The only version today is <c>1</c>.
    /// </remarks>
    public int RulesVersion { get; set; } = 1;

    /// <summary>
    /// The rule body as JSON: a <c>trigger</c> condition tree and the <c>actions</c> to run when it
    /// matches.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The trigger is a group of conditions using <c>all</c> or <c>any</c>, where groups may be nested.
    /// Each condition compares a field from the event using one of the supported operators: <c>=</c>,
    /// <c>not</c>, <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>, <c>in</c>, <c>not in</c>,
    /// <c>cont</c>, <c>not cont</c>, <c>exists</c>, and <c>not exists</c>. The trigger is parsed and
    /// evaluated by the shared condition engine in <c>OpenOrderSystem.Core.Rules</c>.
    /// </para>
    /// <para>
    /// <c>actions</c> lists at least one action entry, each with these keys and no others:
    /// </para>
    /// <list type="bullet">
    /// <item><c>action</c>: an action key from the rule action catalog, such as <c>print.ticket</c>.</item>
    /// <item>
    /// <c>target</c>: the <see cref="Devices.DeviceHead.Key"/> of the device the action works with.
    /// Required for device actions and not allowed for service actions. The device's type must match the
    /// action's target device type.
    /// </item>
    /// <item>
    /// <c>settings</c>: optional. How this rule wants the action done, such as a template name or a fixed
    /// recipient list. Static configuration only: anything that comes from the event is read from the
    /// event when the action runs, never written here. Omitted means an empty object.
    /// </item>
    /// </list>
    /// <example>
    /// <code>
    /// {
    ///   "trigger": { "all": [
    ///     { "field": "origin", "op": "=", "value": "counter" },
    ///     { "field": "paymentType", "op": "in", "value": ["cash", "check"] }
    ///   ]},
    ///   "actions": [
    ///     { "action": "print.ticket", "target": "kitchen-printer-1",
    ///       "settings": { "templateName": "kitchen" } },
    ///     { "action": "email.report",
    ///       "settings": { "templateName": "eod", "recipients": ["manager@example.com"] } }
    ///   ]
    /// }
    /// </code>
    /// </example>
    /// <para>
    /// Defaults to an empty object as a placeholder; a rule must contain a valid trigger and at least one
    /// valid action before it can be saved or evaluated.
    /// </para>
    /// </remarks>
    public string Rules { get; set; } = "{}";
}

/// <summary>
/// Flow mode and behavioral flags for an <see cref="EventRule"/>.
/// </summary>
/// <remarks>
/// <para>
/// Bits 0–3 hold the flow mode: a single value, not combinable, read with
/// <see cref="EventRuleFlagsExtensions.FlowMode"/>. Do not test flow modes with
/// <see cref="Enum.HasFlag"/>; <see cref="Continue"/> is zero and modes share bits.
/// </para>
/// <para>
/// Bits 4 and up are independent flags and may be combined freely.
/// </para>
/// <para>
/// A flow mode applies only when the rule's trigger matches. A rule whose trigger does not
/// match never stops evaluation.
/// </para>
/// </remarks>
[Flags]
public enum EventRuleFlags
{
    /// <summary>
    /// No flags set. Flow mode is <see cref="Continue"/>.
    /// </summary>
    None = 0,

    /// <summary>
    /// Flow mode: continue evaluating the remaining rules. This is the default.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Flow mode: finish evaluating every rule at this rule's priority, then stop.
    /// </summary>
    StopAfterPriority = 1,

    /// <summary>
    /// Flow mode: stop immediately after this rule's actions are queued.
    /// </summary>
    StopAfterRule = 2,

    /// <summary>
    /// Mask covering the flow mode bits (0–3).
    /// </summary>
    FlowMask = 0xF,

    /// <summary>
    /// The rule is kept but skipped during evaluation, allowing it to be switched off
    /// without deleting it.
    /// </summary>
    Disabled = 1 << 4,
}

/// <summary>
/// Helpers for reading <see cref="EventRuleFlags"/>.
/// </summary>
public static class EventRuleFlagsExtensions
{
    /// <summary>
    /// Returns the flow mode portion of the flags.
    /// </summary>
    public static EventRuleFlags FlowMode(this EventRuleFlags flags)
        => flags & EventRuleFlags.FlowMask;

    /// <summary>
    /// Returns <see langword="true"/> if the flow mode is one of the defined modes.
    /// </summary>
    public static bool HasValidFlowMode(this EventRuleFlags flags)
        => flags.FlowMode() is EventRuleFlags.Continue
            or EventRuleFlags.StopAfterPriority
            or EventRuleFlags.StopAfterRule;
}
