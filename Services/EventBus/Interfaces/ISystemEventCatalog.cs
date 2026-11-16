using System.Diagnostics.CodeAnalysis;
using OpenOrderSystem.Core.Services.__BuilderExtensions;

namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// The registered system events, built at startup by
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/>. The source of truth for which events
/// exist.
/// </summary>
/// <remarks>
/// <see cref="ISystemEventBus"/> uses it to find the key of an event being raised. Rule validation uses
/// it to reject rules for unknown events, and the rule editor uses it to fill its event dropdown.
/// </remarks>
public interface ISystemEventCatalog
{
    /// <summary>
    /// Every registered event, ordered by key.
    /// </summary>
    IReadOnlyList<SystemEventDescriptor> Events { get; }

    /// <summary>
    /// Looks up an event by key.
    /// </summary>
    /// <param name="key">The event key. Matched exactly, case-sensitive.</param>
    /// <param name="descriptor">The event, when found.</param>
    /// <returns><see langword="true"/> if an event with that key is registered.</returns>
    bool TryGet(string key, [NotNullWhen(true)] out SystemEventDescriptor? descriptor);

    /// <summary>
    /// Looks up an event by its record type.
    /// </summary>
    /// <param name="eventType">
    /// The event's runtime type. Matched exactly: a type derived from a registered event is not found
    /// unless it declares its own <see cref="SystemEventAttribute"/>.
    /// </param>
    /// <param name="descriptor">The event, when found.</param>
    /// <returns><see langword="true"/> if <paramref name="eventType"/> is a registered event.</returns>
    bool TryGetByType(Type eventType, [NotNullWhen(true)] out SystemEventDescriptor? descriptor);
}

/// <summary>
/// One registered system event, as declared by its <see cref="SystemEventAttribute"/>.
/// </summary>
/// <param name="Key">The event key, such as <c>order.placed</c>.</param>
/// <param name="EventType">The record the event is raised as. Always implements <see cref="ISystemEvent"/>.</param>
/// <param name="DisplayName">The name shown in the rule editor. Falls back to <paramref name="Key"/> when the attribute sets none.</param>
/// <param name="Description">When the event is raised and what it carries. <see langword="null"/> when not given.</param>
public sealed record SystemEventDescriptor(
    string Key,
    Type EventType,
    string DisplayName,
    string? Description);
