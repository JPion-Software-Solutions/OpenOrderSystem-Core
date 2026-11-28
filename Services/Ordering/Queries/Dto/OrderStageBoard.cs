using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Services.Ordering.Queries.Dto;

/// <summary>
/// One stage chain, main or shadow, with its orders sorted into its stages.
/// </summary>
/// <param name="HeaderId">The chain's ID.</param>
/// <param name="Name">The chain's name.</param>
/// <param name="IsShadow">True for a shadow chain, whose stages each cover one or more main-chain stages.</param>
/// <param name="Stages">The chain's stages in chain order, each with its orders.</param>
public sealed record OrderStageBoard(
    Guid HeaderId,
    string Name,
    bool IsShadow,
    IReadOnlyList<OrderStageColumn> Stages);

/// <summary>
/// One stage of an <see cref="OrderStageBoard"/> and the orders in it.
/// </summary>
/// <param name="StageId">The stage's ID.</param>
/// <param name="Name">The stage's name.</param>
/// <param name="Flags">
/// The flags in effect for this stage. On a shadow chain, order-scoped flags are dropped. Whether to act on
/// view-scoped flags, such as <see cref="OrderStageFlags.IsCollapsed"/>, is up to the view.
/// </param>
/// <param name="Orders">The orders in the stage, by time slot and then order number.</param>
public sealed record OrderStageColumn(
    Guid StageId,
    string Name,
    OrderStageFlags Flags,
    IReadOnlyList<OrderSummary> Orders);
