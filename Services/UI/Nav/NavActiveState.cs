// OpenOrderSystem.Core/Services/UI/Nav/NavActiveState.cs
namespace OpenOrderSystem.Core.Services.UI.Nav;

/// <summary>
/// Marks the active item in a built menu. The rules are described on <see cref="NavLocation"/>.
/// </summary>
internal static class NavActiveState
{
    /// <summary>
    /// Returns the menu with <see cref="NavMenuItem.State"/> set, or the menu unchanged when nothing matches.
    /// </summary>
    /// <param name="menu">The visible menu, as built for the user.</param>
    /// <param name="location">Where the user is.</param>
    /// <param name="ownerKeyNotVisible">
    /// Called with <see cref="NavLocation.OwnerKey"/> when it isn't in the visible menu, so the caller
    /// can warn about keys that were never registered. Normal matching is used instead.
    /// </param>
    public static IReadOnlyList<NavMenuItem> Apply(
        IReadOnlyList<NavMenuItem> menu, NavLocation location, Action<string>? ownerKeyNotVisible = null)
    {
        // Every visible item with the keys of the submenus above it, in menu order.
        var entries = new List<(NavMenuItem Item, string[] Ancestors)>();
        Collect(menu, []);

        (NavMenuItem Item, string[] Ancestors)? chosen = null;
        if (location.OwnerKey is { } ownerKey)
        {
            var owner = entries.FindIndex(e => e.Item.Node.Key == ownerKey);
            if (owner >= 0) chosen = entries[owner];
            else ownerKeyNotVisible?.Invoke(ownerKey);
        }

        if (chosen is null)
        {
            var bestScore = 0;
            foreach (var entry in entries)
            {
                var score = Score(entry.Item.Node.Target, location);
                if (score > bestScore) // strictly greater, so ties keep the earlier item
                {
                    bestScore = score;
                    chosen = entry;
                }
            }
        }

        if (chosen is not { } match) return menu;

        var containsActive = new HashSet<string>(match.Ancestors, StringComparer.Ordinal);
        string? activeKey = null;
        if (match.Item.IsSubmenu) containsActive.Add(match.Item.Node.Key); // a page owned by a submenu
        else activeKey = match.Item.Node.Key;

        return Mark(menu);

        void Collect(IReadOnlyList<NavMenuItem> items, string[] ancestors)
        {
            foreach (var item in items)
            {
                entries.Add((item, ancestors));
                if (item.Children.Count > 0) Collect(item.Children, [.. ancestors, item.Node.Key]);
            }
        }

        IReadOnlyList<NavMenuItem> Mark(IReadOnlyList<NavMenuItem> items) =>
            items.Select(item => item with
            {
                State = item.Node.Key == activeKey ? NavItemState.Active
                    : containsActive.Contains(item.Node.Key) ? NavItemState.ContainsActive
                    : NavItemState.None,
                Children = item.Children.Count > 0 ? Mark(item.Children) : item.Children,
            }).ToArray();
    }

    /// <summary>How well a target matches the location; 0 for no match, higher for more specific.</summary>
    private static int Score(NavTarget? target, NavLocation location)
    {
        const int exact = int.MaxValue;
        switch (target)
        {
            case PageRoute page when location.Page is not null && SameArea(page.Area, location.Area):
            {
                if (Same(page.Page, location.Page)) return exact;
                var folder = Folder(page.Page);
                if (folder.Length == 0) return 0; // root pages only match exactly
                if (!location.Page.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)) return 0;
                // Deeper folders win; within a folder, its Index page beats other pages (such as "Add").
                return folder.Count(c => c == '/') * 2 + (IsIndex(page.Page[(folder.Length + 1)..]) ? 1 : 0);
            }
            case ControllerRoute route when location.Controller is not null && SameArea(route.Area, location.Area):
                if (!Same(route.Controller, location.Controller)) return 0;
                if (Same(route.Action, location.Action)) return exact;
                return IsIndex(route.Action) ? 2 : 1; // the controller's Index beats its other actions
            default:
                return 0;
        }
    }

    /// <summary><c>/Products/Edit</c> → <c>/Products</c>; <c>/Index</c> → empty.</summary>
    private static string Folder(string page)
    {
        var slash = page.LastIndexOf('/');
        return slash <= 0 ? string.Empty : page[..slash];
    }

    private static bool IsIndex(string name) => Same(name, "Index");

    private static bool SameArea(string? a, string? b) => Same(a ?? string.Empty, b ?? string.Empty);

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
