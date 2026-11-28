using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Services.Ordering.Stages;

/// <summary>
/// One stage chain, main or shadow, laid out for a stage-level view (staff order terminal, customer status
/// board, etc.), with orders' main-chain stages resolved to stages of this chain.
/// </summary>
/// <remarks>
/// Main and shadow chains are handled the same way: a view shows <see cref="Stages"/> and places each order
/// with <see cref="Resolve"/>. For a main chain every stage resolves to itself.
/// </remarks>
public sealed class OrderStageChainView
{
    private readonly IReadOnlyDictionary<Guid, OrderStage?> _byMainStage;

    private OrderStageChainView(OrderStageHeader header, IReadOnlyList<OrderStage> stages,
        IReadOnlyDictionary<Guid, OrderStage?> byMainStage)
    {
        Header = header;
        Stages = stages;
        _byMainStage = byMainStage;
    }

    /// <summary>The chain being viewed.</summary>
    public OrderStageHeader Header { get; }

    /// <summary>
    /// The chain's stages in chain order, starting at the stage flagged <see cref="OrderStageFlags.IsInitial"/>.
    /// Stages that can't be reached by following the next-stage links from there are left out.
    /// </summary>
    public IReadOnlyList<OrderStage> Stages { get; }

    /// <summary>
    /// Returns the stage of this chain that an order on the given main-chain stage appears in, or
    /// <see langword="null"/> when the order doesn't appear in this chain (not yet active, on a different
    /// main chain, or before the first anchor of a shadow chain).
    /// </summary>
    /// <param name="mainStageId">The order's <c>CurrentStageId</c>.</param>
    public OrderStage? Resolve(Guid? mainStageId)
        => mainStageId is { } id && _byMainStage.TryGetValue(id, out var stage) ? stage : null;

    /// <summary>
    /// The main-chain stages whose orders appear in the given stage of this chain: the inverse of
    /// <see cref="Resolve"/>. For a main chain, that is the stage itself.
    /// </summary>
    /// <param name="stageId">A stage of this chain.</param>
    /// <returns>The covered main-chain stage IDs; empty if the stage is not in <see cref="Stages"/>.</returns>
    public IReadOnlyList<Guid> MainStagesCoveredBy(Guid stageId)
        => _byMainStage.Where(kv => kv.Value?.Id == stageId).Select(kv => kv.Key).ToList();

    /// <summary>
    /// Every main-chain stage whose orders appear somewhere in this chain.
    /// </summary>
    public IReadOnlyList<Guid> CoveredMainStages
        => _byMainStage.Where(kv => kv.Value is not null).Select(kv => kv.Key).ToList();

    /// <summary>
    /// The flags in effect for one of this chain's stages: order-scoped flags are dropped on a shadow chain.
    /// </summary>
    public OrderStageFlags EffectiveFlags(OrderStage stage) => stage.Flags.Effective(Header.IsShadow);

    /// <summary>
    /// Builds a view of <paramref name="header"/>.
    /// </summary>
    /// <param name="header">The chain to view.</param>
    /// <param name="chainStages">All stages belonging to <paramref name="header"/>.</param>
    /// <param name="mainStages">For a shadow chain, all stages of the chain it shadows; ignored for a main chain.</param>
    /// <remarks>
    /// A shadow stage covers its anchor and every following main-chain stage until the next anchor is met.
    /// The main chain is walked in order, so the result doesn't depend on how the shadow chain is ordered.
    /// If two shadow stages share an anchor, the later one in shadow-chain order wins.
    /// </remarks>
    internal static OrderStageChainView Build(OrderStageHeader header, IEnumerable<OrderStage> chainStages,
        IEnumerable<OrderStage> mainStages)
    {
        var stages = Walk(chainStages);
        var byMainStage = new Dictionary<Guid, OrderStage?>();

        if (!header.IsShadow)
        {
            foreach (var stage in stages)
                byMainStage[stage.Id] = stage;
            return new OrderStageChainView(header, stages, byMainStage);
        }

        var byAnchor = new Dictionary<Guid, OrderStage>();
        foreach (var stage in stages)
            if (stage.ShadowAnchorStageId is { } anchorId)
                byAnchor[anchorId] = stage;

        OrderStage? current = null;
        foreach (var mainStage in Walk(mainStages))
        {
            if (byAnchor.TryGetValue(mainStage.Id, out var anchored))
                current = anchored;
            byMainStage[mainStage.Id] = current;
        }

        return new OrderStageChainView(header, stages, byMainStage);
    }

    /// <summary>
    /// Orders a chain's stages by following next-stage links from the initial stage (the stage flagged
    /// <see cref="OrderStageFlags.IsInitial"/>, else the first stage with no previous stage). Stops at the
    /// end of the chain or on a loop.
    /// </summary>
    private static List<OrderStage> Walk(IEnumerable<OrderStage> chainStages)
    {
        var all = chainStages.ToList();
        var byId = all.ToDictionary(s => s.Id);
        var start = all.FirstOrDefault(s => s.Flags.HasFlag(OrderStageFlags.IsInitial))
                    ?? all.FirstOrDefault(s => s.PreviousStageId is null);

        var ordered = new List<OrderStage>();
        var seen = new HashSet<Guid>();
        for (var stage = start; stage is not null && seen.Add(stage.Id);
             stage = stage.NextStageId is { } next ? byId.GetValueOrDefault(next) : null)
            ordered.Add(stage);
        return ordered;
    }
}
