using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents the header record for a customer order, tracking its progression
/// through the fulfillment workflow and associating it with its line items.
/// </summary>
public class OrderHeader
{
    private Dictionary<string, JsonElement>? _metadataCache;
    private string? _metadataJsonCache;

    /// <summary>
    /// The unique identifier for this order.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The human-readable order number, reset daily. Use <see cref="OrderDate"/> for
    /// full uniqueness when referencing historical orders.
    /// </summary>
    public int OrderNumber { get; set; }

    /// <summary>
    /// The date and time the order was placed, including local timezone offset.
    /// Used for daily order number partitioning and chronological sorting.
    /// </summary>
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>
    /// The identifier of the stage this order is currently in.
    /// </summary>
    public Guid? CurrentStageId { get; set; } = null;

    /// <summary>
    /// Navigation property for the current stage in the fulfillment workflow.
    /// </summary>
    public OrderStage? CurrentStage { get; set; } = null;

    /// <summary>
    /// The identifier of the customer record associated with this order, or
    /// <see langword="null"/> if the customer record has been purged per the
    /// configured retention policy.
    /// </summary>
    public Guid? CustomerRecordId { get; set; } = null;

    /// <summary>
    /// Navigation property for the associated customer record.
    /// </summary>
    public CustomerRecord? CustomerRecord { get; set; } = null;

    /// <summary>
    /// Optional comments or special instructions provided by the customer at time of ordering.
    /// </summary>
    public string? OrderComments { get; set; }

    /// <summary>
    /// The date and time at which this order should automatically advance to the next stage,
    /// or <see langword="null"/> if no automatic advancement is scheduled.
    /// Set automatically from <see cref="OrderStage.AutoCompleteTime"/> on stage entry,
    /// but may be overridden manually.
    /// </summary>
    public DateTimeOffset? StageAdvanceTime { get; set; } = null;

    /// <summary>
    /// The line items associated with this order.
    /// </summary>
    public List<OrderLine> LineItems { get; set; } = new();

    /// <summary>
    /// The stage transition history for this order.
    /// </summary>
    public List<OrderStageHistory> StageHistory { get; set; } = new();

    /// <summary>
    /// Behavioral and state flags for this order.
    /// </summary>
    /// <seealso cref="OrderHeaderFlags"/>
    public OrderHeaderFlags Flags { get; set; } = OrderHeaderFlags.None;
    
    /// <summary>
    /// The assigned fulfillment slot time for this order. For pre-orders this reflects the
    /// customer's requested time snapped to the nearest available slot. For immediate orders
    /// this is the next available slot at the time of placement.
    /// </summary>
    /// <remarks>
    /// This value is always set by the order service at placement time. The default is a
    /// placeholder only — <see cref="DateTimeOffset.UtcNow"/> — and should never be persisted
    /// without the service layer assigning an appropriate slot.
    /// </remarks>
    public DateTimeOffset AssignedTimeSlot { get; set; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Identifies the channel or integration through which this order was placed
    /// (e.g., "web", "in-store", "phone", "voip-agent").
    /// </summary>
    /// <remarks>
    /// Origin-specific extension data that does not require querying or indexing
    /// may be stored in <see cref="MetadataJson"/>.
    /// </remarks>
    public string OrderOrigin { get; set; } = string.Empty;

    /// <summary>
    /// JSON storage for <see cref="Metadata"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Use metadata helper methods to keep cached state coherent and to avoid persisting invalid JSON.
    /// </remarks>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Arbitrary extension metadata for this order.
    /// </summary>
    /// <remarks>
    /// Metadata is intended for non-core extension fields such as UI hints, plugin or vendor-specific data,
    /// and future promotional system integration. Values that require querying, indexing, or core validation
    /// should be promoted to first-class columns.
    /// </remarks>
    [NotMapped]
    public IReadOnlyDictionary<string, JsonElement> Metadata => EnsureMetadataLoaded();
    
    /// <summary>
    /// Sets a metadata key to the specified JSON value.
    /// </summary>
    /// <param name="key">Metadata key. Cannot be null or whitespace.</param>
    /// <param name="value">JSON value to store.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
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
    /// <typeparam name="T">Value type to serialize.</typeparam>
    /// <param name="key">Metadata key. Cannot be null or whitespace.</param>
    /// <param name="value">Value to serialize and store.</param>
    public void SetMetadata<T>(string key, T value)
        => SetMetadata(key, JsonSerializer.SerializeToElement(value));

    /// <summary>
    /// Removes a metadata key if present.
    /// </summary>
    /// <param name="key">Metadata key to remove.</param>
    /// <returns><see langword="true"/> if the key existed and was removed; otherwise <see langword="false"/>.</returns>
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
/// Records a single stage transition in an order's fulfillment history.
/// </summary>
public class OrderStageHistory
{
    /// <summary>
    /// The unique identifier for this history entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The identifier of the stage this entry refers to.
    /// </summary>
    public Guid StageId { get; set; }

    /// <summary>
    /// The date and time the order entered this stage.
    /// </summary>
    public DateTimeOffset EnteredAt { get; set; }

    /// <summary>
    /// The date and time the order left this stage, or <see langword="null"/> if this is the current stage.
    /// </summary>
    public DateTimeOffset? ExitedAt { get; set; } = null;
}

/// <summary>
/// Behavioral and state flags for an <see cref="OrderHeader"/>.
/// </summary>
[Flags]
public enum OrderHeaderFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates the order was cancelled. If set alone, the order was cancelled before
    /// any cost was incurred. Combine with <see cref="IsWaste"/> or <see cref="IsComp"/>
    /// to express the nature of the loss.
    /// </summary>
    IsCanceled = 1 << 0,

    /// <summary>
    /// Indicates the order resulted in a loss due to waste such as a dropped or spoiled item.
    /// May be combined with <see cref="IsCanceled"/> if the waste was triggered by a cancellation
    /// after the order was already in progress.
    /// </summary>
    IsWaste = 1 << 1,

    /// <summary>
    /// Indicates the order was comped as a goodwill gesture or to resolve a customer complaint.
    /// May be combined with <see cref="IsCanceled"/> if the order was cancelled and written off,
    /// or with <see cref="IsWaste"/> if a replacement was comped after a waste event.
    /// </summary>
    IsComp = 1 << 2,

    /// <summary>
    /// Indicates the order is queued for future fulfillment and is not yet part of the active workflow.
    /// Advancement into the active workflow is controlled by <see cref="OrderHeader.StageAdvanceTime"/>.
    /// </summary>
    IsQueued = 1 << 3,

    /// <summary>
    /// Indicates the order was successfully fulfilled. Distinct from a terminal stage, which may
    /// also be reached via cancellation or comp.
    /// </summary>
    IsFulfilled = 1 << 4,
}