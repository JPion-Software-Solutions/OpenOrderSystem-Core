using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// Declares a class as a rule action handler, giving the key rules use to name it, the kind of work it
/// does, and the device type and settings it works with.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place an action's key is declared.
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/> finds every non-abstract class
/// with this attribute, registers it as a keyed <see cref="IRuleActionHandler"/>, and adds it to the
/// <see cref="IRuleActionCatalog"/> that rule validation and the rule editor read.
/// </para>
/// <para>
/// The class must implement <see cref="IRuleActionHandler"/> and the key must be unique and not blank.
/// <see cref="TargetDeviceType"/> is required on <see cref="RuleActionKind.Device"/> actions and not
/// allowed on <see cref="RuleActionKind.Service"/> actions. Breaking any of these, or naming an invalid
/// <see cref="TargetDeviceType"/> or <see cref="SettingsType"/>, fails startup.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [RuleAction("print.ticket", RuleActionKind.Device,
///     DisplayName = "Print Ticket",
///     Description = "Renders an order ticket from a template and queues it for a printer.",
///     TargetDeviceType = typeof(PrinterDeviceType),
///     SettingsType = typeof(PrintTicketSettings))]
/// public class PrintTicketAction : IRuleActionHandler { }
/// </code>
/// </example>
/// <param name="key">The action key rules use, such as <c>print.ticket</c>.</param>
/// <param name="kind">Whether the action queues device work or performs server-side work.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RuleActionAttribute(string key, RuleActionKind kind) : Attribute
{
    /// <summary>
    /// The action key rules use to name this handler, such as <c>print.ticket</c>.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Whether the action queues device work or performs server-side work.
    /// </summary>
    public RuleActionKind Kind { get; } = kind;

    /// <summary>
    /// The human-friendly name shown in the rule editor's action dropdown. When not set, consumers
    /// fall back to <see cref="Key"/>.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// What the action does, for help text or tooltips in the rule editor.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The device type definition a device action works with, such as
    /// <c>typeof(PrinterDeviceType)</c>. Lets the rule editor filter its device picker and lets rule
    /// validation reject targets of the wrong type.
    /// </summary>
    /// <remarks>
    /// Required on <see cref="RuleActionKind.Device"/> actions and not allowed on
    /// <see cref="RuleActionKind.Service"/> actions. The type must carry <see cref="DeviceTypeAttribute"/>
    /// and implement <see cref="IDeviceTypeDefinition"/>, or startup fails. Its
    /// <see cref="DeviceTypeAttribute.Key"/> is compared against
    /// <see cref="Data.DataModels.V2.Devices.DeviceHead.DeviceType"/> of a rule's target device.
    /// </remarks>
    public Type? TargetDeviceType { get; set; }

    /// <summary>
    /// The record a rule's <c>settings</c> for this action are read into, or <see langword="null"/> if the
    /// action takes no settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be a non-abstract class or record, or startup fails. Rule validation rejects keys the type
    /// does not declare, deserializes each rule's settings for this action into it with
    /// <see cref="RuleActionSettings.SettingsOptions"/>, and runs <see cref="IValidatableSettings.Validate"/>
    /// when it implements that, so a rule that would fail at runtime fails when it is saved. The handler
    /// reads it with <see cref="RuleActionSettings.TryReadSettings{TSettings}"/>, which uses the same
    /// options.
    /// </para>
    /// <para>
    /// At startup, <c>{}</c> is deserialized into the type with
    /// <see cref="RuleActionSettings.SettingsOptions"/>. Failing only because required properties are
    /// missing is fine; any other failure, such as a type with more than one public constructor and no
    /// <c>[JsonConstructor]</c>, or a constructor parameter that matches no property, fails startup. So
    /// deserializing settings later can only fail for bad input, never for a broken type.
    /// </para>
    /// <para>
    /// That check only covers the top-level type. A broken type nested inside it, such as a list item
    /// record with two constructors, is only found when a rule's settings reach it; rule validation and
    /// <see cref="RuleActionSettings.TryReadSettings{TSettings}"/> then report it as a settings error at
    /// <c>actions[n].settings</c> instead of throwing. Likewise, only top-level keys are checked one by
    /// one: an unknown key inside a nested object is reported as one error at <c>actions[n].settings</c>,
    /// not at its own path.
    /// </para>
    /// <para>
    /// The type's constructors must not throw. Checks on the values belong in
    /// <see cref="IValidatableSettings.Validate"/>, where they are reported as rule errors.
    /// </para>
    /// <para>
    /// When not set, a rule's <c>settings</c> for this action must be omitted or an empty object.
    /// </para>
    /// </remarks>
    public Type? SettingsType { get; set; }
}
