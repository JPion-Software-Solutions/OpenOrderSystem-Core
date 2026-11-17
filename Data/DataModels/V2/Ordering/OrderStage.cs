using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a single stage in an order's progression through the fulfillment workflow.
/// </summary>
/// <remarks>
/// <para>
/// Stages are organized as doubly-linked lists, where each stage references its predecessor and
/// successor within its own chain (<see cref="Header"/>). Each chain is anchored by
/// <see cref="OrderStageFlags.IsInitial"/> and <see cref="OrderStageFlags.IsTerminal"/> flags on its
/// endpoint stages. Previous/next links never cross chains.
/// </para>
/// <para>
/// A stage in a shadow chain (see <see cref="OrderStageHeader"/>) sets <see cref="ShadowAnchorStageId"/> and
/// covers the main-chain stages from its anchor up to, but not including, the next anchor met walking the
/// main chain. Orders never sit on a shadow stage, and shadow stages ignore order-scoped flags and
/// <see cref="AutoCompleteTime"/>.
/// </para>
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
    /// The chain this stage belongs to.
    /// </summary>
    public Guid HeaderId { get; set; }

    /// <summary>
    /// Navigation property for <see cref="HeaderId"/>.
    /// </summary>
    public OrderStageHeader? Header { get; set; } = null;

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
    /// Shadow-chain stages only: the main-chain stage where this stage's coverage begins. This stage covers
    /// the anchor and every following main-chain stage up to the next anchor. <see langword="null"/> on
    /// main-chain stages; on a shadow stage it means the stage covers nothing.
    /// </summary>
    /// <remarks>
    /// Deleting the anchor stage clears this link (client-side set null: the shadow stages must be loaded
    /// in the deleting context).
    /// </remarks>
    public Guid? ShadowAnchorStageId { get; set; } = null;

    /// <summary>
    /// Navigation property for <see cref="ShadowAnchorStageId"/>.
    /// </summary>
    public OrderStage? ShadowAnchorStage { get; set; } = null;

    /// <summary>
    /// The duration in minutes after which an order in this stage is automatically advanced
    /// to the next stage, or <see langword="null"/> to require manual progression.
    /// Ignored on shadow-chain stages.
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
/// <remarks>
/// Every flag has one scope, given in its documentation and collected by the masks in
/// <see cref="OrderStageFlagsExtensions"/>:
/// <list type="bullet">
/// <item><b>Structural</b>: describes the chain itself. Applies to main and shadow chains.</item>
/// <item><b>Order-scoped</b>: acts on an individual order as it enters or leaves the stage. Applies only
/// to main-chain stages; ignored on shadow-chain stages, since orders never sit on them.</item>
/// <item><b>View-scoped</b>: acts on a stage-level view (staff order terminal, customer status board,
/// etc.) showing the stage's chain. Applies to main and shadow chains.</item>
/// </list>
/// A new flag must pick a scope and be added to the matching mask.
/// </remarks>
[Flags]
public enum OrderStageFlags
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Structural. Marks this as the first stage in its chain. Orders enter a main chain at this stage.
    /// </summary>
    IsInitial = 1 << 0,

    /// <summary>
    /// Structural. Marks this as the last stage in its chain. Orders reaching this stage of a main chain
    /// are considered complete.
    /// </summary>
    IsTerminal = 1 << 1,

    /// <summary>
    /// Order-scoped. Prevents further modification or cancellation of an order once it reaches this stage.
    /// </summary>
    IsOrderLocked = 1 << 2,

    /// <summary>
    /// Order-scoped. Triggers an order ticket to be sent to the configured printer when an order enters this stage.
    /// </summary>
    PrintsOrderTicket = 1 << 3,

    /// <summary>
    /// View-scoped. Plays an audible cue on any view showing this stage's chain when an order appears in
    /// this stage.
    /// </summary>
    PlaysAudibleSound = 1 << 4,  
    
    /// <summary>
    /// View-scoped. Orders in this stage aren't shown by default. A view that can reveal them on request
    /// (e.g. an expandable section on a staff terminal) may do so; a view that can't (e.g. a customer pickup
    /// board) doesn't show them.
    /// </summary>
    IsCollapsed = 1 << 7,

    /// <summary>
    /// Order-scoped. Sends a customer notification when an order enters this stage.
    /// </summary>
    NotifiesCustomerOnStart = 1 << 5,

    /// <summary>
    /// Order-scoped. Sends a customer notification when an order leaves this stage.
    /// </summary>
    NotifiesCustomerOnFinish = 1 << 6,
}

/// <summary>
/// Scope masks for <see cref="OrderStageFlags"/>. Consumers filter with these instead of keeping their own
/// lists of flags.
/// </summary>
public static class OrderStageFlagsExtensions
{
    /// <summary>
    /// Flags that describe the chain itself. Apply to main and shadow chains.
    /// </summary>
    public const OrderStageFlags StructuralMask = OrderStageFlags.IsInitial | OrderStageFlags.IsTerminal;

    /// <summary>
    /// Flags that act on an individual order. Apply only to main-chain stages.
    /// </summary>
    public const OrderStageFlags OrderScopedMask = OrderStageFlags.IsOrderLocked
                                                 | OrderStageFlags.PrintsOrderTicket
                                                 | OrderStageFlags.NotifiesCustomerOnStart
                                                 | OrderStageFlags.NotifiesCustomerOnFinish;

    /// <summary>
    /// Flags that act on a stage-level view showing the stage's chain. Apply to main and shadow chains.
    /// </summary>
    public const OrderStageFlags ViewScopedMask = OrderStageFlags.PlaysAudibleSound | OrderStageFlags.IsCollapsed;

    /// <summary>
    /// Returns the flags that are in effect for a stage, dropping order-scoped flags on a shadow-chain stage.
    /// </summary>
    /// <param name="flags">The stage's flags.</param>
    /// <param name="isShadowStage"><see langword="true"/> when the stage belongs to a shadow chain.</param>
    public static OrderStageFlags Effective(this OrderStageFlags flags, bool isShadowStage)
        => isShadowStage ? flags & ~OrderScopedMask : flags;
}
