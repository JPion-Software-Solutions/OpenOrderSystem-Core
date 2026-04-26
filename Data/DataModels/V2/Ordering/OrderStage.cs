using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a single stage in an order's progression through the fulfillment workflow.
/// </summary>
/// <remarks>
/// Stages are organized as a doubly-linked list, where each stage references its
/// predecessor and successor. The chain is anchored by <see cref="OrderStageFlags.IsInitial"/>
/// and <see cref="OrderStageFlags.IsTerminal"/> flags on the respective endpoint stages.
/// </remarks>
public class OrderStage
{
    private Dictionary<string, JsonElement>? _metadataCache;
    private string? _metadataJsonCache;
    
    /// <summary>
    /// The unique identifier for this stage.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The display name of this stage (e.g., "In Progress", "Ready for Pickup").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable description of what this stage represents in the fulfillment workflow.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The identifier of the preceding stage in the workflow chain, or <see langword="null"/> if this is the initial stage.
    /// </summary>
    public Guid? PreviousStageId { get; set; } = null;

    /// <summary>
    /// Navigation property for the preceding stage in the workflow chain.
    /// </summary>
    public OrderStage? PreviousStage { get; set; } = null;

    /// <summary>
    /// The identifier of the following stage in the workflow chain, or <see langword="null"/> if this is the terminal stage.
    /// </summary>
    public Guid? NextStageId { get; set; } = null;

    /// <summary>
    /// Navigation property for the following stage in the workflow chain.
    /// </summary>
    public OrderStage? NextStage { get; set; } = null;

    /// <summary>
    /// The duration in minutes after which an order in this stage is automatically advanced
    /// to the next stage, or <see langword="null"/> to require manual progression.
    /// </summary>
    public decimal? AutoCompleteTime { get; set; } = null;

    /// <summary>
    /// Behavioral flags controlling how this stage interacts with the order workflow,
    /// customer and staff visibility, and system integrations.
    /// </summary>
    /// <seealso cref="OrderStageFlags"/>
    public OrderStageFlags Flags { get; set; } = OrderStageFlags.None;
    
    /// <summary>
    /// When <see langword="true"/>, this stage cannot be deleted.
    /// All other properties remain configurable.
    /// </summary>
    /// <remarks>
    /// Protected stages are seeded by the bootstrapper to guarantee the stage chain
    /// always has a valid entry and exit point. At least one stage should carry
    /// <see cref="OrderStageFlags.IsInitial"/> and one should carry
    /// <see cref="OrderStageFlags.IsTerminal"/> among the protected stages.
    /// </remarks>
    public bool IsProtected { get; set; } = false;

    /// <summary>
    /// JSON storage for <see cref="Metadata"/>.
    /// </summary>
    /// <remarks>
    /// <b>Warning:</b> Writing to this property directly is not recommended.
    /// Use metadata helper methods to keep cached state coherent and to avoid persisting invalid JSON.
    /// </remarks>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Arbitrary extension metadata for this stage.
    /// </summary>
    /// <remarks>
    /// Metadata is intended for non-core extension fields such as UI hints, appearance configuration,
    /// and plugin or vendor-specific data. Values that require querying, indexing, or core validation
    /// should be promoted to first-class columns.
    /// </remarks>
    [NotMapped]
    public IReadOnlyDictionary<string, JsonElement> Metadata => EnsureMetadataLoaded();

    /// <summary>
    /// Ensures metadata is loaded and returns the current cached representation.
    /// </summary>
    /// <returns>A read-only view of the current metadata dictionary.</returns>
    private IReadOnlyDictionary<string, JsonElement> EnsureMetadataLoaded()
        => EnsureMetadataLoadedMutable();

    /// <summary>
    /// Ensures metadata is loaded into a mutable dictionary and returns it.
    /// </summary>
    /// <remarks>
    /// This method manages cache coherence between <see cref="MetadataJson"/> and the in-memory <see cref="_metadataCache"/>.
    /// </remarks>
    private Dictionary<string, JsonElement> EnsureMetadataLoadedMutable()
    {
        // If we have a cache and the JSON hasn't changed, we're good.
        if (_metadataCache is not null && _metadataJsonCache == MetadataJson)
            return _metadataCache;

        // If nothing stored, use empty.
        if (string.IsNullOrWhiteSpace(MetadataJson))
        {
            _metadataCache = _metadataCache ?? new Dictionary<string, JsonElement>();
            _metadataJsonCache = MetadataJson; // both null/whitespace
            return _metadataCache;
        }

        // Try to parse fresh.
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

    /// <summary>
    /// Persists the provided metadata dictionary into <see cref="MetadataJson"/> and updates internal caches.
    /// </summary>
    /// <param name="dict">Metadata dictionary to persist.</param>
    /// <remarks>
    /// Stores <see langword="null"/> when empty to avoid persisting an empty JSON object (<c>{}</c>).
    /// </remarks>
    private void PersistMetadata(Dictionary<string, JsonElement> dict)
    {
        // Optional: if empty, store NULL instead of "{}"
        if (dict.Count == 0)
        {
            MetadataJson = null;
            _metadataJsonCache = null;
            _metadataCache = dict;
            return;
        }

        MetadataJson = JsonSerializer.Serialize(dict);
        _metadataJsonCache = MetadataJson; // keep cache coherent
        _metadataCache = dict;
    }
}

/// <summary>
/// Behavioral flags for an <see cref="OrderStage"/>, controlling workflow position,
/// order mutability, system integrations, and visibility to customers and staff.
/// </summary>
[Flags]
public enum OrderStageFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Marks this as the first stage in the workflow chain. Orders enter the pipeline at this stage.
    /// </summary>
    IsInitial = 1 << 0,

    /// <summary>
    /// Marks this as the last stage in the workflow chain. Orders reaching this stage are considered complete.
    /// </summary>
    IsTerminal = 1 << 1,

    /// <summary>
    /// Prevents further modification or cancellation of an order once it reaches this stage.
    /// </summary>
    IsOrderLocked = 1 << 2,

    /// <summary>
    /// Triggers an order ticket to be sent to the configured printer when an order enters this stage.
    /// </summary>
    PrintsOrderTicket = 1 << 3,

    /// <summary>
    /// Triggers an audible alert on the configured output device when an order enters this stage.
    /// </summary>
    PlaysAudibleSound = 1 << 4,

    /// <summary>
    /// Sends a customer notification when an order enters this stage.
    /// </summary>
    NotifiesCustomerOnStart = 1 << 5,

    /// <summary>
    /// Sends a customer notification when an order leaves this stage.
    /// </summary>
    NotifiesCustomerOnFinish = 1 << 6,

    /// <summary>
    /// Hides this stage from customer-facing views. When set, the stage collapses to the nearest
    /// previous visible stage by default, or the next visible stage if <see cref="CollapseUp"/> is also set.
    /// </summary>
    IsHiddenFromCustomers = 1 << 7,

    /// <summary>
    /// Hides this stage from staff-facing views. When set, the stage collapses to the nearest
    /// previous visible stage by default, or the next visible stage if <see cref="CollapseUp"/> is also set.
    /// </summary>
    IsHiddenFromStaff = 1 << 8,

    /// <summary>
    /// Modifies the collapse direction for hidden stages. When combined with <see cref="IsHiddenFromCustomers"/>
    /// or <see cref="IsHiddenFromStaff"/>, the stage is represented by the next visible stage rather than
    /// the previous one. Has no effect if neither hidden flag is set.
    /// </summary>
    CollapseUp = 1 << 9,
}