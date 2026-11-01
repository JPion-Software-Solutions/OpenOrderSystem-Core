// OpenOrderSystem.Core/Services/UI/Nav/NavNode.cs
namespace OpenOrderSystem.Core.Services.UI.Nav;

/// <summary>
/// Where a <see cref="NavNode"/> goes when clicked. One of <see cref="PageRoute"/>,
/// <see cref="ControllerRoute"/>, <see cref="ExternalLink"/> or <see cref="FormLink"/>.
/// </summary>
public abstract record NavTarget;

/// <summary>
/// A destination inside OOS, addressed through routing rather than by path so the link keeps working
/// when URLs, areas or the app's base path change. Either a <see cref="PageRoute"/> or a <see cref="ControllerRoute"/>.
/// </summary>
/// <param name="Area">Area name (as in <c>asp-area</c>), or <c>null</c> for no area.</param>
/// <param name="RouteValues">Extra route values (such as an <c>id</c>), or <c>null</c> for none.</param>
public abstract record InternalRoute(
    string? Area,
    IReadOnlyDictionary<string, string>? RouteValues) : NavTarget;

/// <summary>A Razor Page inside OOS.</summary>
/// <param name="Page">
/// Page name as in <c>asp-page</c>: the path under the area's <c>Pages</c> folder, starting with <c>/</c>
/// and without <c>.cshtml</c> (for example <c>/Users/Index</c>).
/// </param>
/// <param name="Area">Area name (as in <c>asp-area</c>), or <c>null</c> for a page outside any area.</param>
/// <param name="RouteValues">Extra route values (such as an <c>id</c>), or <c>null</c> for none.</param>
public sealed record PageRoute(
    string Page,
    string? Area = null,
    IReadOnlyDictionary<string, string>? RouteValues = null) : InternalRoute(Area, RouteValues);

/// <summary>An MVC controller action inside OOS.</summary>
/// <param name="Controller">Controller name, without the <c>Controller</c> suffix (as in <c>asp-controller</c>).</param>
/// <param name="Action">Action name (as in <c>asp-action</c>).</param>
/// <param name="Area">Area name (as in <c>asp-area</c>), or <c>null</c> for a controller outside any area.</param>
/// <param name="RouteValues">Extra route values (such as an <c>id</c>), or <c>null</c> for none.</param>
public sealed record ControllerRoute(
    string Controller,
    string Action,
    string? Area = null,
    IReadOnlyDictionary<string, string>? RouteValues = null) : InternalRoute(Area, RouteValues);

/// <summary>A link to a page outside OOS.</summary>
/// <param name="Url">The absolute URL to open.</param>
public sealed record ExternalLink(Uri Url) : NavTarget;

/// <summary>
/// A nav item that submits a POST instead of following a link, for actions such as logging out
/// that must not be triggered by a GET.
/// </summary>
/// <param name="HandlerPath">The path the form posts to.</param>
/// <param name="FormData">
/// Fixed hidden fields sent with the post, or <c>null</c> for none. The nav never collects input
/// from the user; these values are set when the node is defined.
/// </param>
public sealed record FormLink(string HandlerPath, IReadOnlyDictionary<string, string>? FormData = null) : NavTarget;

/// <summary>
/// One item in a navigation menu. Nodes form a tree through <see cref="ParentKey"/>.
/// </summary>
public sealed class NavNode
{
    /// <summary>
    /// Identifier for this node, unique within the menu. Child nodes refer to it through
    /// <see cref="ParentKey"/>.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The text shown for the item.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// A semantic icon key (see <see cref="Theme.Icons.IconKeys"/>), resolved through the icon providers.
    /// Empty for no icon.
    /// </summary>
    public string IconKey { get; init; } = string.Empty;

    /// <summary>
    /// Where the item goes, or <c>null</c> if the item is a submenu: it opens to show the nodes whose
    /// <see cref="ParentKey"/> is this node's <see cref="Key"/> and goes nowhere itself.
    /// </summary>
    public NavTarget? Target { get; init; }

    /// <summary>The <see cref="Key"/> of the submenu this node sits in, or <c>null</c> for a top-level item.</summary>
    public string? ParentKey { get; init; }

    /// <summary>
    /// Sort position among siblings, lowest first. Siblings with the same value are sorted
    /// alphabetically by <see cref="Label"/>.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Name of the authorization policy the user must satisfy to see this item. Empty for no check.
    /// </summary>
    /// <remarks>
    /// Cosmetic only: it hides links to pages the user can't open. The controller or page behind the
    /// target must enforce access itself.
    /// </remarks>
    public string Policy { get; init; } = string.Empty;
}
