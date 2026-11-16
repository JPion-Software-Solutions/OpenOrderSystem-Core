using System.Diagnostics.CodeAnalysis;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// The in-memory <see cref="IRuleActionCatalog"/>, built once by
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/> from the actions found in the loaded
/// assemblies. Read-only.
/// </summary>
internal sealed class RuleActionCatalog : IRuleActionCatalog
{
    private readonly Dictionary<string, RuleActionDescriptor> _byKey;

    /// <summary>
    /// Creates the catalog. Keys must already be unique; registration checks that first.
    /// </summary>
    internal RuleActionCatalog(IEnumerable<RuleActionDescriptor> descriptors)
    {
        _byKey = descriptors.ToDictionary(d => d.Key, StringComparer.Ordinal);
        Actions = _byKey.Values.OrderBy(d => d.Key, StringComparer.Ordinal).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<RuleActionDescriptor> Actions { get; }

    /// <inheritdoc />
    public bool TryGet(string key, [NotNullWhen(true)] out RuleActionDescriptor? descriptor) =>
        _byKey.TryGetValue(key, out descriptor);
}
