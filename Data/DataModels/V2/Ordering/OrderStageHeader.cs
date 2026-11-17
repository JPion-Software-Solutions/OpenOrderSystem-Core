namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Gives a stage chain its identity. Every <see cref="OrderStage"/> belongs to exactly one header.
/// </summary>
/// <remarks>
/// <para>
/// A header is either a <b>main chain</b> (<see cref="ShadowOfHeaderId"/> is <see langword="null"/>) or a
/// <b>shadow chain</b> of a main chain. Orders only ever sit on main-chain stages; a main chain is what
/// orders move along, and its order-scoped flags (see <see cref="OrderStageFlagsExtensions.OrderScopedMask"/>)
/// are what act on them.
/// </para>
/// <para>
/// A shadow chain is a second, usually coarser, view of its main chain, e.g. a customer status board that
/// shows "In progress" and "Ready" instead of every prep stage. Its stages form an ordinary linked chain of
/// their own, so views walk and display it exactly like a main chain. Each shadow stage covers a run of
/// main-chain stages starting at its <see cref="OrderStage.ShadowAnchorStageId"/>; an order's stage in the
/// shadow chain is resolved from its main-chain stage, never stored.
/// </para>
/// <para>
/// A shadow chain must shadow a main chain; shadows of shadows are not supported.
/// </para>
/// </remarks>
public class OrderStageHeader
{
    /// <summary>
    /// The unique identifier for this chain.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The display name of this chain (e.g., "Pickup", "Customer board").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable description of what this chain is for.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The main chain this chain shadows, or <see langword="null"/> if this is a main chain.
    /// Must reference a main chain.
    /// </summary>
    public Guid? ShadowOfHeaderId { get; set; } = null;

    /// <summary>
    /// Navigation property for <see cref="ShadowOfHeaderId"/>.
    /// </summary>
    public OrderStageHeader? ShadowOf { get; set; } = null;

    /// <summary>
    /// The shadow chains of this chain. Always empty for a shadow chain.
    /// </summary>
    public List<OrderStageHeader> Shadows { get; set; } = new();

    /// <summary>
    /// The stages that make up this chain, in no particular order. Use the stages' previous/next links
    /// for chain order.
    /// </summary>
    public List<OrderStage> Stages { get; set; } = new();

    /// <summary>
    /// When <see langword="true"/>, this chain cannot be deleted. All other properties remain configurable.
    /// </summary>
    public bool IsProtected { get; set; } = false;

    /// <summary>
    /// <see langword="true"/> when this is a shadow chain.
    /// </summary>
    public bool IsShadow => ShadowOfHeaderId is not null;
}
