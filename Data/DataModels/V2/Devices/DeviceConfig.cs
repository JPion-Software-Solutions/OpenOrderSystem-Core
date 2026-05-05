namespace OpenOrderSystem.Core.Data.DataModels.V2.Devices;

/// <summary>
/// Represents a single configuration entry for a <see cref="DeviceHead"/>.
/// </summary>
/// <remarks>
/// <para>
/// The composite primary key of <see cref="DeviceId"/> and <see cref="Key"/> enforces
/// uniqueness of configuration keys per device at the database level.
/// </para>
/// <para>
/// By convention, configuration keys should be namespaced using the owning device's
/// <see cref="DeviceHead.Key"/> as a prefix (e.g., "printer-kitchen-1.template.receipt")
/// to avoid collisions when querying across devices.
/// </para>
/// </remarks>
public class DeviceConfig
{
    /// <summary>
    /// Foreign key to the owning <see cref="DeviceHead"/>.
    /// </summary>
    public Guid DeviceId { get; set; }

    /// <summary>
    /// Navigation property for the owning device.
    /// </summary>
    public DeviceHead? Device { get; set; }

    /// <summary>
    /// Configuration key. Must be unique per device.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Configuration value stored as a string. Complex values should be serialized as JSON.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}