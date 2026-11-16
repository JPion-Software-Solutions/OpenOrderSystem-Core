using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// The <see cref="ISystemEventBus"/> that writes events to the <see cref="OutboxEvent"/> table of the
/// caller's own context. Registered as a singleton by
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/>.
/// </summary>
public sealed class OutboxSystemEventBus(ISystemEventCatalog catalog) : ISystemEventBus
{
    private readonly ISystemEventCatalog _catalog = catalog;

    /// <inheritdoc />
    public Guid Raise<TEvent>(OosDbContext db, TEvent evt) where TEvent : ISystemEvent
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(evt);

        var descriptor = Describe(evt);
        var row = CreateRow(Guid.CreateVersion7(), descriptor, evt);
        db.OutboxEvents.Add(row);
        return row.Id;
    }

    /// <inheritdoc />
    public async Task<RaiseOnceResult> RaiseOnceAsync<TEvent>(OosDbContext db, TEvent evt, string dedupeKey,
        CancellationToken ct = default) where TEvent : ISystemEvent
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupeKey);

        var descriptor = Describe(evt);
        var id = DeterministicId(descriptor.Key, dedupeKey);

        // FindAsync checks entities the context already tracks, including ones only added, before
        // querying the database.
        if (await db.OutboxEvents.FindAsync([id], ct) is not null)
            return new RaiseOnceResult(id, Raised: false);

        db.OutboxEvents.Add(CreateRow(id, descriptor, evt));
        return new RaiseOnceResult(id, Raised: true);
    }

    /// <summary>
    /// The deterministic Id <see cref="RaiseOnceAsync{TEvent}"/> uses: a UUID version 8 (RFC 9562) from
    /// the first 16 bytes of SHA-256 over the UTF-8 of <c>&lt;eventKey&gt;:&lt;dedupeKey&gt;</c>, with
    /// the version and variant bits set and the bytes read in big-endian (RFC) order.
    /// </summary>
    /// <param name="eventKey">The event's key, such as <c>business-day.closed</c>.</param>
    /// <param name="dedupeKey">The occurrence, such as <c>2026-11-16</c>.</param>
    /// <returns>The same Id for the same pair, every time and on every platform.</returns>
    public static Guid DeterministicId(string eventKey, string dedupeKey)
    {
        ArgumentNullException.ThrowIfNull(eventKey);
        ArgumentNullException.ThrowIfNull(dedupeKey);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{eventKey}:{dedupeKey}"), hash);

        var bytes = hash[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }

    private SystemEventDescriptor Describe(ISystemEvent evt)
    {
        var type = evt.GetType();
        return _catalog.TryGetByType(type, out var descriptor)
            ? descriptor
            : throw new InvalidOperationException(
                $"{type.FullName} is not a registered system event. Event types need [SystemEvent(\"key\")], must implement {nameof(ISystemEvent)}, and their assembly must be loaded before {nameof(EventBusBuilderExtensions.AddOosEventBusServices)} runs.");
    }

    private static OutboxEvent CreateRow(Guid id, SystemEventDescriptor descriptor, ISystemEvent evt) => new()
    {
        Id = id,
        EventKey = descriptor.Key,
        Payload = JsonSerializer.Serialize(evt, descriptor.EventType, RuleActionSettings.EventOptions),
        OccurredAt = DateTimeOffset.UtcNow,
    };
}
