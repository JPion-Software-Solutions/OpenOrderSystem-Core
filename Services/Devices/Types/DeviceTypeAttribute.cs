using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Services.Devices.Types;

/// <summary>
/// Declares a class as a device type definition, giving the key devices of this type are recorded
/// with and whether the type is principal or dependent.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place a device type's key is declared.
/// <see cref="DevicesBuilderExtensions.AddOosDeviceServices"/> finds every non-abstract class with this
/// attribute in the loaded assemblies and adds it to the <see cref="IDeviceTypeCatalog"/>, which is the
/// source of truth for which device types exist. A plugin declares its own device types the same way and
/// only needs to reference Core.
/// </para>
/// <para>
/// The class must implement <see cref="IDeviceTypeDefinition"/>, and the key must be unique, not blank,
/// and at most <see cref="Data.DataModels.V2.Devices.DeviceHead.DeviceTypeMaxLength"/> characters.
/// Breaking any of these fails startup.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [DeviceType("printer", DeviceRole.Dependent,
///     DisplayName = "Printer",
///     Description = "A receipt or ticket printer driven by a print bridge.")]
/// public class PrinterDeviceType : IDeviceTypeDefinition { }
/// </code>
/// </example>
/// <param name="key">
/// The device type key, such as <c>printer</c>. Stored in
/// <see cref="Data.DataModels.V2.Devices.DeviceHead.DeviceType"/>. A device action names the device type by
/// its class in <see cref="EventBus.RuleActionAttribute.TargetDeviceType"/>, which resolves to this key.
/// </param>
/// <param name="role">Whether devices of this type talk to OOS directly or through a parent.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DeviceTypeAttribute(string key, DeviceRole role) : Attribute
{
    /// <summary>
    /// The device type key, such as <c>printer</c>.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Whether devices of this type talk to OOS directly or through a parent.
    /// </summary>
    public DeviceRole Role { get; } = role;

    /// <summary>
    /// The human-friendly name shown in the admin area. When not set, consumers fall back to
    /// <see cref="Key"/>.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// What this kind of device is, for help text in the admin area.
    /// </summary>
    public string? Description { get; set; }
}
