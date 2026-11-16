namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// A group of conditions combined with <c>all</c> (every child must match) or <c>any</c> (at least one
/// child must match). Groups may be nested.
/// </summary>
/// <remarks>
/// An empty <c>all</c> group always matches, and an empty <c>any</c> group never does.
/// </remarks>
/// <param name="RequireAll">
/// <see langword="true"/> for an <c>all</c> group, <see langword="false"/> for an <c>any</c> group.
/// </param>
/// <param name="Children">The group's conditions and nested groups, in their JSON order.</param>
public sealed record ConditionGroup(bool RequireAll, IReadOnlyList<ConditionNode> Children) : ConditionNode;
