using System.ComponentModel.DataAnnotations;
using OpenOrderSystem.Core.Data.DataModels.V2.Devices;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Devices;

/// <summary>
/// Represents a registered device or integration endpoint in the OOS device registry.
/// </summary>
/// <remarks>
/// <para>
/// Devices are organized as a tree rooted at the <c>oos-root</c> device, which is
/// automatically enrolled on first boot. All other devices are descendants of this root,
/// either directly or through intermediate parent devices such as a PrintBridge driving
/// multiple physical printers.
/// </para>
/// <para>
/// Device type is a string discriminator managed by the system via device handler discovery.
/// It is not operator-configurable. External plugins may register their own device types
/// by implementing the device handler interface and decorating it with the device info attribute.
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
    /// Unique identifier for this device.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-friendly unique key for this device (e.g., "printer-kitchen-1", "oos-root").
    /// Used as a secondary index for lookups and as the namespace prefix for
    /// <see cref="DeviceConfig"/> keys associated with this device.
    /// </summary>
    /// <remarks>
    /// Keys must be unique across all registered devices. By convention, keys should be
    /// lowercase and hyphen-separated. The key is system-assigned or operator-defined
    /// at enrollment time and should not change after registration.
    /// </remarks>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name for this device (e.g., "Kitchen Printer", "Front Counter Terminal").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// System-managed type discriminator identifying the device handler responsible for
    /// managing this device's IO interactions with OOS.
    /// </summary>
    /// <remarks>
    /// This field is written by the device handler discovery system and is not operator-configurable.
    /// Core device types are defined in the OOS core assembly. External plugins may register
    /// additional device types by implementing the device handler interface.
    /// </remarks>
    [MaxLength(100)]
    public string DeviceType { get; set; } = string.Empty;

    /// <summary>
    /// The identifier of this device's parent in the device tree, or <see langword="null"/>
    /// for the root device (<c>oos-root</c>).
    /// </summary>
    public Guid? ParentId { get; set; } = null;

    /// <summary>
    /// Navigation property for this device's parent in the device tree.
    /// </summary>
    public DeviceHead? Parent { get; set; } = null;

    /// <summary>
    /// Child devices registered under this device in the device tree.
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
    /// Custom flag slot 1. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom1 = 1 << 16,

    /// <summary>
    /// Custom flag slot 2. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom2 = 1 << 17,

    /// <summary>
    /// Custom flag slot 3. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom3 = 1 << 18,

    /// <summary>
    /// Custom flag slot 4. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom4 = 1 << 19,

    /// <summary>
    /// Custom flag slot 5. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom5 = 1 << 20,

    /// <summary>
    /// Custom flag slot 6. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom6 = 1 << 21,

    /// <summary>
    /// Custom flag slot 7. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom7 = 1 << 22,

    /// <summary>
    /// Custom flag slot 8. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom8 = 1 << 23,

    /// <summary>
    /// Custom flag slot 9. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom9 = 1 << 24,

    /// <summary>
    /// Custom flag slot 10. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom10 = 1 << 25,

    /// <summary>
    /// Custom flag slot 11. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom11 = 1 << 26,

    /// <summary>
    /// Custom flag slot 12. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom12 = 1 << 27,

    /// <summary>
    /// Custom flag slot 13. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom13 = 1 << 28,

    /// <summary>
    /// Custom flag slot 14. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom14 = 1 << 29,

    /// <summary>
    /// Custom flag slot 15. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom15 = 1 << 30,

    /// <summary>
    /// Custom flag slot 16. Semantics are defined by the device handler or plugin.
    /// </summary>
    Custom16 = 1 << 31,
}