// OpenOrderSystem.Core/Services/Devices/DeviceContext.cs
using OpenOrderSystem.Core.Data.DataModels.V2.Devices;

namespace OpenOrderSystem.Core.Services.Devices;

/// <summary>
/// Represents a flattened, read-only snapshot of a <see cref="DeviceHead"/> and its
/// associated <see cref="DeviceConfig"/> entries, provided to device handlers at
/// command dispatch time.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DeviceContext"/> gives handlers immediate access to device identity,
/// configuration, and tree relationships without requiring additional database calls
/// or navigation property traversal.
/// </para>
/// <para>
/// Instances should be created via <see cref="Create"/> rather than direct construction.
/// </para>
/// </remarks>
public class DeviceContext
{
    /// <summary>
    /// The unique identifier of this device.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The human-friendly unique key of this device (e.g., <c>"printer-kitchen-1"</c>).
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// The display name of this device.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The system-managed device type discriminator identifying this device's handler.
    /// </summary>
    public string DeviceType { get; init; } = string.Empty;

    /// <summary>
    /// The unique identifier of this device's parent in the device tree,
    /// or <see langword="null"/> for <c>oos-root</c>.
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// The key of this device's parent in the device tree,
    /// or <see langword="null"/> for <c>oos-root</c>.
    /// </summary>
    public string? ParentKey { get; init; }

    /// <summary>
    /// The keys of all immediate child devices registered under this device
    /// in the device tree.
    /// </summary>
    public IReadOnlyList<string> ChildKeys { get; init; } = [];

    /// <summary>
    /// Behavioral flags for this device.
    /// </summary>
    public DeviceFlags Flags { get; init; }

    /// <summary>
    /// Flattened configuration entries for this device, keyed by configuration key.
    /// </summary>
    /// <remarks>
    /// Values are stored as strings. Complex values are serialized as JSON.
    /// Keys follow the namespacing convention of the owning device's
    /// <see cref="Key"/> as a prefix (e.g., <c>"printer-kitchen-1.template.receipt"</c>).
    /// </remarks>
    public IReadOnlyDictionary<string, string> Config { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Creates a new <see cref="DeviceContext"/> from the given <see cref="DeviceHead"/>,
    /// flattening its configuration and tree relationships into a single read-only snapshot.
    /// </summary>
    /// <param name="device">
    /// The <see cref="DeviceHead"/> to create the context from. Must have
    /// <see cref="DeviceHead.Config"/>, <see cref="DeviceHead.Parent"/>, and
    /// <see cref="DeviceHead.Children"/> navigation properties populated.
    /// </param>
    /// <returns>A new <see cref="DeviceContext"/> representing the given device.</returns>
    public static DeviceContext Create(DeviceHead device) => new()
    {
        Id = device.Id,
        Key = device.Key,
        Name = device.Name,
        DeviceType = device.DeviceType,
        ParentId = device.ParentId,
        ParentKey = device.Parent?.Key,
        ChildKeys = device.Children.Select(c => c.Key).ToList().AsReadOnly(),
        Flags = device.Flags,
        Config = device.Config.ToDictionary(c => c.Key, c => c.Value)
    };
}