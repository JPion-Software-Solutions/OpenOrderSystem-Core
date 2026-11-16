using System.Diagnostics.CodeAnalysis;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Services.Devices.Types;

/// <summary>
/// The in-memory <see cref="IDeviceTypeCatalog"/>, built once by
/// <see cref="DevicesBuilderExtensions.AddOosDeviceServices"/> from the device types found in the
/// loaded assemblies. Read-only.
/// </summary>
internal sealed class DeviceTypeCatalog : IDeviceTypeCatalog
{
    private readonly Dictionary<string, DeviceTypeDescriptor> _byKey;

    /// <summary>
    /// Creates the catalog. Keys must already be unique; registration checks that first.
    /// </summary>
    internal DeviceTypeCatalog(IEnumerable<DeviceTypeDescriptor> descriptors)
    {
        _byKey = descriptors.ToDictionary(d => d.Key, StringComparer.Ordinal);
        Types = _byKey.Values.OrderBy(d => d.Key, StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<DeviceTypeDescriptor> Types { get; }

    /// <inheritdoc />
    public bool TryGet(string key, [NotNullWhen(true)] out DeviceTypeDescriptor? descriptor) =>
        _byKey.TryGetValue(key, out descriptor);
}
