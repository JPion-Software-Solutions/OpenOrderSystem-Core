using NuGet.Packaging;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Interfaces.Catalog;

/// <summary>
/// Represents a hierarchical "group" node that can contain member entities and child groups of the same type.
/// </summary>
/// <remarks>
/// <para>
/// This interface models a classic adjacency-list tree (optional <see cref="ParentId"/> / <see cref="Parent"/> with
/// a collection of <see cref="Children"/>).
/// </para>
/// <para>
/// Group membership is represented by <see cref="Members"/> and the corresponding <see cref="IGroupMember{TGroup,TMember}"/>
/// implementation on the member entity. Membership is optional at the database level (members may have a null GroupId).
/// </para>
/// <para>
/// The default <see cref="GetMembers(int)"/> implementation returns a flattened list of members from this group and,
/// optionally, from its descendants up to a specified depth. The implementation intentionally returns a new list to avoid
/// mutating navigation collections (important for EF Core change tracking).
/// </para>
/// </remarks>
/// <typeparam name="TGroup">
/// The concrete group type (self-referential generic parameter).
/// </typeparam>
/// <typeparam name="TMember">
/// The concrete member type contained by this group.
/// </typeparam>
public interface IGroupable<TGroup, TMember>
    where TGroup  : class, IGroupable<TGroup, TMember>
    where TMember : class, IGroupMember<TGroup, TMember>
{
    /// <summary>
    /// Unique identifier for this group node.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// Human-readable name of the group.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Optional descriptive text for the group (e.g., UI help text).
    /// </summary>
    string Description { get; set; }

    /// <summary>
    /// Optional foreign key to this group's parent node. When null, the group is a top-level/root node.
    /// </summary>
    Guid? ParentId { get; set; }

    /// <summary>
    /// Optional navigation reference to this group's parent node.
    /// </summary>
    TGroup? Parent { get; set; }

    /// <summary>
    /// Optional collection of child groups whose <see cref="ParentId"/> points to this group.
    /// </summary>
    ICollection<TGroup>? Children { get; set; }

    /// <summary>
    /// Optional collection of member entities assigned to this group.
    /// </summary>
    /// <remarks>
    /// Membership is optional: a member may have a null GroupId and therefore appear in no group's Members collection.
    /// </remarks>
    ICollection<TMember>? Members { get; set; }

    /// <summary>
    /// Sort order hint for displaying sibling groups within the same parent.
    /// Lower values should appear earlier unless overridden by the application.
    /// </summary>
    int SortPriority { get; set; }

    /// <summary>
    /// Returns a flattened list of this group's members, optionally including members from descendants.
    /// </summary>
    /// <param name="depth">
    /// The maximum descendant depth to include.
    /// <list type="bullet">
    /// <item><description><c>0</c>: only this group's direct <see cref="Members"/>.</description></item>
    /// <item><description><c>1</c>: include direct children.</description></item>
    /// <item><description><c>2</c>: include grandchildren, etc.</description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// A new list containing members found within the specified depth. This method never mutates navigation collections.
    /// </returns>
    IReadOnlyList<TMember> GetMembers(int depth = 0)
    {
        var results = new List<TMember>();

        if (Members != null)
            results.AddRange(Members);

        if (depth <= 0)
            return results;

        if (Children != null)
        {
            foreach (var child in Children)
                results.AddRange(child.GetMembers(depth - 1));
        }

        return results;
    }
}

/// <summary>
/// Represents an entity that may optionally belong to a group.
/// </summary>
/// <remarks>
/// <para>
/// This interface is the inverse side of the <see cref="IGroupable{TGroup,TMember}"/> relationship.
/// </para>
/// <para>
/// Group membership is optional: <see cref="GroupId"/> may be null, which indicates the entity is "ungrouped".
/// Applications may represent ungrouped entities in the UI using a synthetic "Unassigned" bucket without persisting a
/// special system group.
/// </para>
/// </remarks>
/// <typeparam name="TGroup">
/// The concrete group type that may contain this member.
/// </typeparam>
/// <typeparam name="TMember">
/// The concrete member type (self-referential generic parameter).
/// </typeparam>
public interface IGroupMember<TGroup, TMember>
    where TGroup  : class, IGroupable<TGroup, TMember>
    where TMember : class, IGroupMember<TGroup, TMember>
{
    /// <summary>
    /// Optional foreign key to the group this entity belongs to. When null, the entity is ungrouped.
    /// </summary>
    Guid? GroupId { get; set; }

    /// <summary>
    /// Optional navigation reference to the group this entity belongs to.
    /// </summary>
    TGroup? Group { get; set; }
}
