// OpenOrderSystem.Core/Services/UI/Nav/NavMenuExtensions.cs
namespace OpenOrderSystem.Core.Services.UI.Nav;

/// <summary>Helpers for reading a built menu.</summary>
public static class NavMenuExtensions
{
    /// <summary>
    /// The path from the top of the menu down to the current page: each <see cref="NavItemState.ContainsActive"/>
    /// submenu in order, ending with the <see cref="NavItemState.Active"/> item if there is one.
    /// </summary>
    /// <param name="menu">A menu built with a location, as returned by <c>GetMenuAsync</c>.</param>
    /// <returns>
    /// The trail, top first. Empty when nothing is active, or when the menu was built without a location.
    /// </returns>
    /// <remarks>
    /// The trail ends at the nav item, not the page. On a page that isn't in the menu (one that declares
    /// its owner with <see cref="NavOwnerAttribute"/>, or matched by folder), the renderer should append
    /// the page's own title after the last crumb.
    /// </remarks>
    public static IReadOnlyList<NavMenuItem> GetBreadcrumbs(this IReadOnlyList<NavMenuItem> menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        var trail = new List<NavMenuItem>();
        var level = menu;
        while (level.FirstOrDefault(item => item.State != NavItemState.None) is { } item)
        {
            trail.Add(item);
            if (item.State == NavItemState.Active) break;
            level = item.Children;
        }
        return trail;
    }
}
