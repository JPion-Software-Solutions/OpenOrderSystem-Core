using System.Diagnostics.CodeAnalysis;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// The in-memory <see cref="ISystemEventCatalog"/>, built once by
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/> from the events found in the loaded
/// assemblies. Read-only.
/// </summary>
internal sealed class SystemEventCatalog : ISystemEventCatalog
{
    private readonly Dictionary<string, SystemEventDescriptor> _byKey;
    private readonly Dictionary<Type, SystemEventDescriptor> _byType;

    /// <summary>
    /// Creates the catalog. Keys must already be unique; registration checks that first.
    /// </summary>
    internal SystemEventCatalog(IEnumerable<SystemEventDescriptor> descriptors)
    {
        _byKey = descriptors.ToDictionary(d => d.Key, StringComparer.Ordinal);
        _byType = _byKey.Values.ToDictionary(d => d.EventType);
        Events = _byKey.Values.OrderBy(d => d.Key, StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<SystemEventDescriptor> Events { get; }

    /// <inheritdoc />
    public bool TryGet(string key, [NotNullWhen(true)] out SystemEventDescriptor? descriptor) =>
        _byKey.TryGetValue(key, out descriptor);

    /// <inheritdoc />
    public bool TryGetByType(Type eventType, [NotNullWhen(true)] out SystemEventDescriptor? descriptor) =>
        _byType.TryGetValue(eventType, out descriptor);
}
