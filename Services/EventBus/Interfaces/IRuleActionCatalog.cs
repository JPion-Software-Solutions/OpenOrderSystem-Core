using OpenOrderSystem.Core.Services.__BuilderExtensions;

namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// The registered rule actions, built at startup by
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/>.
/// </summary>
/// <remarks>
/// Rule validation uses it to check that a rule's actions exist, that device actions target the right
/// device type, and that each action's settings have the right shape. The rule editor uses it to fill
/// its action dropdown. It describes actions only; the action worker resolves handlers by key from the
/// service provider.
/// </remarks>
public interface IRuleActionCatalog
{
    /// <summary>
    /// Every registered action, ordered by key.
    /// </summary>
    IReadOnlyList<RuleActionDescriptor> Actions { get; }

    /// <summary>
    /// Looks up an action by key.
    /// </summary>
    /// <param name="key">The action key. Matched exactly, case-sensitive.</param>
    /// <param name="descriptor">The action, when found.</param>
    /// <returns><see langword="true"/> if an action with that key is registered.</returns>
    bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out RuleActionDescriptor? descriptor);
}

/// <summary>
/// One registered rule action, as declared by its <see cref="RuleActionAttribute"/>.
/// </summary>
/// <param name="Key">The action key rules use.</param>
/// <param name="Kind">Whether the action queues device work or performs server-side work.</param>
/// <param name="DisplayName">The name shown in the rule editor. Falls back to <paramref name="Key"/> when the attribute sets none.</param>
/// <param name="Description">What the action does, for help text. <see langword="null"/> when not given.</param>
/// <param name="TargetDeviceType">
/// The device type definition a device action works with, from
/// <see cref="RuleActionAttribute.TargetDeviceType"/>. Always set for device actions and always
/// <see langword="null"/> for service actions.
/// </param>
/// <param name="TargetDeviceTypeKey">
/// The <see cref="Devices.Types.DeviceTypeAttribute.Key"/> of <paramref name="TargetDeviceType"/>, which a
/// target device's <see cref="Data.DataModels.V2.Devices.DeviceHead.DeviceType"/> must equal. Set exactly
/// when <paramref name="TargetDeviceType"/> is.
/// </param>
/// <param name="SettingsType">
/// The record the action's settings are read into, from <see cref="RuleActionAttribute.SettingsType"/>.
/// <see langword="null"/> when the action takes no settings.
/// </param>
public sealed record RuleActionDescriptor(
    string Key,
    RuleActionKind Kind,
    string DisplayName,
    string? Description,
    Type? TargetDeviceType,
    string? TargetDeviceTypeKey,
    Type? SettingsType);
