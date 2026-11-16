using System.ComponentModel.DataAnnotations;
using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Devices;

/// <summary>
/// Represents a registered device or integration endpoint in the OOS device registry.
/// </summary>
/// <remarks>
/// <para>
/// Devices form trees through <see cref="ParentId"/>. Every tree has exactly one principal device (see
/// <see cref="DeviceRole.Principal"/>), and it is the root of that tree: a principal device never has a
/// parent. Every other device in the tree is a dependent (see <see cref="DeviceRole.Dependent"/>), at any
/// depth, such as printers under a print bridge. A dependent device's jobs are claimed by the principal
/// device at the root of its tree.
/// </para>
/// <para>
/// <see cref="DeviceType"/> is the key of a device type declared with <see cref="DeviceTypeAttribute"/>
/// and listed in the <see cref="IDeviceTypeCatalog"/>. It is not operator-configurable. External plugins
/// add their own device types the same way core does.
/// </para>
/// <para>
/// Operational configuration for a device is stored in <see cref="Config"/> rather than
/// a JSON metadata column, allowing individual configuration entries to be queried and
/// updated independently of the device record itself.
/// </para>
/// </remarks>
public class DeviceHead
{
    /// <summary>
    /// The longest <see cref="DeviceType"/> the database column holds. Device type keys longer than this
    /// fail startup.
    /// </summary>
    public const int DeviceTypeMaxLength = 100;

    /// <summary>
    /// Unique identifier for this device.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-friendly unique key for this device (e.g., "printer-kitchen-1").
    /// Used as a secondary index for lookups and as the namespace prefix for
    /// <see cref="DeviceConfig"/> keys associated with this device.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keys must be unique across all registered devices. By convention, keys should be
    /// lowercase and hyphen-separated. The key is set when the device is created and is immutable after
    /// that; change <see cref="Name"/> to rename a device.
    /// </para>
    /// <para>
    /// Event rules name the devices their actions target by key, so a key that changed would silently
    /// break every rule that refers to it.
    /// </para>
    /// </remarks>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable display name for this device (e.g., "Kitchen Printer", "Front Counter Terminal").
    /// Can change at any time without affecting rules, which refer to the device by <see cref="Key"/>.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The key of this device's type, such as <c>printer</c>, as declared by its
    /// <see cref="DeviceTypeAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Must be a key in the <see cref="IDeviceTypeCatalog"/>. Set by the system when the device is
    /// enrolled and not operator-configurable. Core device types are declared in the OOS core assembly;
    /// plugins may declare more. Also decides whether the device is principal or dependent, through
    /// <see cref="DeviceTypeAttribute.Role"/>.
    /// </remarks>
    [MaxLength(DeviceTypeMaxLength)]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// The identifier of this device's parent in the device tree, or <see langword="null"/>
    /// for the principal device at the root of its tree.
    /// </summary>
    /// <remarks>
    /// Always <see langword="null"/> for a principal device, and always set for a dependent device.
    /// </remarks>
    public Guid? ParentId { get; set; } = null;

    /// <summary>
    /// Navigation property for this device's parent in the device tree.
    /// </summary>
    public DeviceHead? Parent { get; set; } = null;

    /// <summary>
    /// Child devices registered under this device in the device tree. Always dependent devices.
    /// </summary>
    /// <remarks>
    /// For example, a PrintBridge device may have multiple physical printer devices as children.
    /// </remarks>
    public List<DeviceHead> Children { get; set; } = new();

    /// <summary>
    /// Behavioral flags for this device.
    /// </summary>
    /// <seealso cref="DeviceFlags"/>
    public DeviceFlags Flags { get; set; } = DeviceFlags.None;

    /// <summary>
    /// Operational configuration entries for this device.
    /// </summary>
    /// <remarks>
    /// Configuration is stored as discrete key-value rows rather than a JSON blob,
    /// allowing individual entries to be queried and updated independently.
    /// </remarks>
    public List<DeviceConfig> Config { get; set; } = new();
}

/// <summary>
/// Behavioral flags for a <see cref="DeviceHead"/>.
/// </summary>
[Flags]
public enum DeviceFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Custom flag slot 1. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom1 = 1 << 16,

    /// <summary>
    /// Custom flag slot 2. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom2 = 1 << 17,

    /// <summary>
    /// Custom flag slot 3. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom3 = 1 << 18,

    /// <summary>
    /// Custom flag slot 4. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom4 = 1 << 19,

    /// <summary>
    /// Custom flag slot 5. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom5 = 1 << 20,

    /// <summary>
    /// Custom flag slot 6. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom6 = 1 << 21,

    /// <summary>
    /// Custom flag slot 7. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom7 = 1 << 22,

    /// <summary>
    /// Custom flag slot 8. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom8 = 1 << 23,

    /// <summary>
    /// Custom flag slot 9. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom9 = 1 << 24,

    /// <summary>
    /// Custom flag slot 10. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom10 = 1 << 25,

    /// <summary>
    /// Custom flag slot 11. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom11 = 1 << 26,

    /// <summary>
    /// Custom flag slot 12. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom12 = 1 << 27,

    /// <summary>
    /// Custom flag slot 13. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom13 = 1 << 28,

    /// <summary>
    /// Custom flag slot 14. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom14 = 1 << 29,

    /// <summary>
    /// Custom flag slot 15. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom15 = 1 << 30,

    /// <summary>
    /// Custom flag slot 16. Semantics are defined by the device type or plugin.
    /// </summary>
    Custom16 = 1 << 31,
}