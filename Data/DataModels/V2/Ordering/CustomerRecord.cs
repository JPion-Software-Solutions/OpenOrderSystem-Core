using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a customer record associated with one or more orders.
/// </summary>
/// <remarks>
/// <para>
/// Customer records are subject to a configurable data retention policy and may be purged
/// after a defined period. Orders retain their association via a nullable foreign key, so
/// purged customer records do not affect order history integrity.
/// </para>
/// <para>
/// <b>Warning:</b> Writing to <see cref="MetadataJson"/> directly is not recommended.
/// Use metadata helper methods to keep cached state coherent and to avoid persisting invalid JSON.
/// </para>
/// </remarks>
public class CustomerRecord
{
    private Dictionary<string, JsonElement>? _metadataCache;
    private string? _metadataJsonCache;

    /// <summary>
    /// Unique identifier for this customer record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Full name of the customer.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Phone number of the customer.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the customer.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The date and time this customer record was created, including local timezone offset.
    /// </summary>
    public DateTimeOffset CustomerCreated { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The customer's preferred notification channels.
    /// </summary>
    /// <seealso cref="CustomerNotificationPreferences"/>
    public CustomerNotificationPreferences NotificationPreferences { get; set; } = CustomerNotificationPreferences.None;

    /// <summary>
    /// JSON storage for <see cref="Metadata"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Use metadata helper methods to keep cached state coherent and to avoid persisting invalid JSON.
    /// </remarks>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Arbitrary extension metadata for this customer record.
    /// </summary>
    /// <remarks>
    /// Metadata is intended for non-core extension fields such as loyalty plugin data,
    /// vendor-specific identifiers, and UI hints. Values that require querying, indexing,
    /// or core validation should be promoted to first-class columns.
    /// </remarks>
    [NotMapped]
    public IReadOnlyDictionary<string, JsonElement> Metadata => EnsureMetadataLoaded();

    /// <summary>
    /// Sets a metadata key to the specified JSON value.
    /// </summary>
    public void SetMetadata(string key, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be null/empty.", nameof(key));

        var dict = EnsureMetadataLoadedMutable();
        dict[key] = value;
        PersistMetadata(dict);
    }

    /// <summary>
    /// Serializes the provided value and stores it as metadata under the specified key.
    /// </summary>
    public void SetMetadata<T>(string key, T value)
        => SetMetadata(key, JsonSerializer.SerializeToElement(value));

    /// <summary>
    /// Removes a metadata key if present.
    /// </summary>
    public bool RemoveMetadata(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var dict = EnsureMetadataLoadedMutable();
        if (!dict.Remove(key)) return false;

        PersistMetadata(dict);
        return true;
    }

    private IReadOnlyDictionary<string, JsonElement> EnsureMetadataLoaded()
        => EnsureMetadataLoadedMutable();

    private Dictionary<string, JsonElement> EnsureMetadataLoadedMutable()
    {
        if (_metadataCache is not null && _metadataJsonCache == MetadataJson)
            return _metadataCache;

        if (string.IsNullOrWhiteSpace(MetadataJson))
        {
            _metadataCache ??= new Dictionary<string, JsonElement>();
            _metadataJsonCache = MetadataJson;
            return _metadataCache;
        }

        try
        {
            _metadataCache = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(MetadataJson)
                             ?? new Dictionary<string, JsonElement>();
        }
        catch
        {
            _metadataCache = new Dictionary<string, JsonElement>();
        }

        _metadataJsonCache = MetadataJson;
        return _metadataCache;
    }

    private void PersistMetadata(Dictionary<string, JsonElement> dict)
    {
        if (dict.Count == 0)
        {
            MetadataJson = null;
            _metadataJsonCache = null;
            _metadataCache = dict;
            return;
        }

        MetadataJson = JsonSerializer.Serialize(dict);
        _metadataJsonCache = MetadataJson;
        _metadataCache = dict;
    }
}

/// <summary>
/// Flags representing a customer's preferred notification channels.
/// </summary>
[Flags]
public enum CustomerNotificationPreferences
{
    /// <summary>
    /// No notification preferences set. The customer will not receive any order updates.
    /// </summary>
    None = 0,

    /// <summary>
    /// The customer prefers to receive order updates via email.
    /// </summary>
    Email = 1 << 0,

    /// <summary>
    /// The customer prefers to receive order updates via SMS.
    /// </summary>
    SMS = 1 << 1,
}