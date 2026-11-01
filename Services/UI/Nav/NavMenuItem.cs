// OpenOrderSystem.Core/Services/UI/Nav/NavMenuItem.cs
namespace OpenOrderSystem.Core.Services.UI.Nav;

/// <summary>
/// One item of a menu built for a specific user: a registered <see cref="NavNode"/> plus the
/// children that user can see, already sorted.
/// </summary>
/// <param name="Node">The registered node.</param>
/// <param name="Children">
/// Visible children, sorted. Empty for a link. A submenu always has at least one, because
/// submenus with nothing visible are left out of the menu.
/// </param>
public sealed record NavMenuItem(NavNode Node, IReadOnlyList<NavMenuItem> Children)
{
    /// <summary>True when the item is a submenu (<see cref="NavNode.Target"/> is <c>null</c>).</summary>
    public bool IsSubmenu => Node.Target is null;

    /// <summary>Whether this item is the current page, or a submenu leading to it.</summary>
    public NavItemState State { get; init; } = NavItemState.None;
}

/// <summary>How a <see cref="NavMenuItem"/> relates to the current page.</summary>
public enum NavItemState
{
    /// <summary>Not related to the current page.</summary>
    None,

    /// <summary>The link for the current page. At most one item in a menu is active.</summary>
    Active,

    /// <summary>
    /// A submenu on the path to the active item, or the submenu a page declared as its owner
    /// with <see cref="NavOwnerAttribute"/>. Typically rendered expanded.
    /// </summary>
    ContainsActive,
}
