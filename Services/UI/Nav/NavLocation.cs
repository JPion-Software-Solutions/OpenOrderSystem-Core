// OpenOrderSystem.Core/Services/UI/Nav/NavLocation.cs
using Microsoft.AspNetCore.Http;

namespace OpenOrderSystem.Core.Services.UI.Nav;

/// <summary>
/// Where the user is, used to decide which nav item is active. Plain values, so menu building
/// doesn't depend on <see cref="HttpContext"/>; use <see cref="From(HttpContext)"/> in views.
/// </summary>
/// <param name="Area">The current area, or <c>null</c> for none.</param>
/// <param name="Page">The current Razor Page name (for example <c>/Products/Edit</c>), or <c>null</c> on a controller action.</param>
/// <param name="Controller">The current controller name, or <c>null</c> on a Razor Page.</param>
/// <param name="Action">The current action name, or <c>null</c> on a Razor Page.</param>
/// <param name="OwnerKey">The key from the endpoint's <see cref="NavOwnerAttribute"/>, if it has one.</param>
/// <remarks>
/// <para>Matching rules, in order:</para>
/// <list type="number">
/// <item><see cref="OwnerKey"/>, if it names an item visible in the menu.</item>
/// <item>
/// Otherwise the most specific matching link. A <see cref="PageRoute"/> matches pages in the same area whose
/// page is the same, or sits in the link's folder or below it; an exact page beats a folder, and a deeper
/// folder beats a shallower one. A link to a page at the root of an area (such as <c>/Index</c>) only
/// matches exactly, so it doesn't claim every page in the area. A <see cref="ControllerRoute"/> matches
/// the same area and controller; the exact action beats any other action. When only the folder or
/// controller matches, a link to its <c>Index</c> beats other links there (such as an "Add" page).
/// Route values are ignored. Remaining ties go to the item that comes first in the menu.
/// </item>
/// <item><see cref="ExternalLink"/> and <see cref="FormLink"/> items are never active.</item>
/// </list>
/// <para>Names are compared case-insensitively, as routing does.</para>
/// </remarks>
public sealed record NavLocation(
    string? Area = null,
    string? Page = null,
    string? Controller = null,
    string? Action = null,
    string? OwnerKey = null)
{
    /// <summary>Reads the location from the current request's route values and endpoint metadata.</summary>
    public static NavLocation From(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var route = context.Request.RouteValues;
        return new NavLocation(
            Area: route["area"] as string,
            Page: route["page"] as string,
            Controller: route["controller"] as string,
            Action: route["action"] as string,
            OwnerKey: context.GetEndpoint()?.Metadata.GetMetadata<NavOwnerAttribute>()?.Key);
    }
}
