using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// Declares a record as a system event, giving the key rules and the outbox use to name it.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place an event's key is declared.
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/> finds every non-abstract class with
/// this attribute and adds it to the <see cref="ISystemEventCatalog"/>, which
/// <see cref="ISystemEventBus"/> uses to find the key when an event is raised, and rule validation uses
/// to reject rules for events that do not exist.
/// </para>
/// <para>
/// The class must implement <see cref="ISystemEvent"/>, and the key must be unique, not blank, and at
/// most <see cref="OutboxEvent.EventKeyMaxLength"/> characters. Breaking any of these fails startup. The
/// attribute is not inherited, so a record derived from an event is not an event unless it declares its
/// own key.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [SystemEvent("order.placed",
///     DisplayName = "Order Placed",
///     Description = "A new order was placed, from any channel.")]
/// public sealed record OrderPlaced(Guid OrderId, int OrderNumber) : ISystemEvent;
/// </code>
/// </example>
/// <param name="key">
/// The event key, such as <c>order.placed</c>: lowercase, dot-separated, naming what happened.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SystemEventAttribute(string key) : Attribute
{
    /// <summary>
    /// The event key, such as <c>order.placed</c>. Stored in <see cref="OutboxEvent.EventKey"/> and
    /// matched against <see cref="EventRule.EventKey"/>.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// The human-friendly name shown in the rule editor's event dropdown. When not set, consumers fall
    /// back to <see cref="Key"/>.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// When the event is raised and what it carries, for help text or tooltips in the rule editor.
    /// </summary>
    public string? Description { get; set; }
}
