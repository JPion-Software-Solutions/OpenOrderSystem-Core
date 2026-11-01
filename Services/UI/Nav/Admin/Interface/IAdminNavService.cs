// OpenOrderSystem.Core/Services/UI/Nav/Admin/Interface/IAdminNavService.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace OpenOrderSystem.Core.Services.UI.Nav.Admin.Interface;

/// <summary>
/// The registry of admin navigation items. Core and plugins register <see cref="NavNode"/>s, and
/// the admin layout asks for the menu a given user can see.
/// </summary>
public interface IAdminNavService
{
    /// <summary>
    /// A snapshot of every registered node, in registration order. Changing the returned
    /// collection doesn't affect the registry; use <see cref="RegisterNode"/> to add nodes.
    /// </summary>
    IReadOnlyCollection<NavNode> NavNodes { get; }

    /// <summary>
    /// Adds a node to the admin menu.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><see cref="NavNode.Key"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">
    /// A node with the same <see cref="NavNode.Key"/> is already registered; or
    /// <see cref="NavNode.ParentKey"/> names a node that hasn't been registered yet (parents must be
    /// registered before their children); or the parent isn't a submenu (it has a <see cref="NavNode.Target"/>).
    /// </exception>
    /// <remarks>
    /// Keys are compared case-sensitively. Plugins should namespace their keys (<c>pluginid:name</c>)
    /// so they can't collide with core or with each other.
    /// </remarks>
    void RegisterNode(NavNode node);

    /// <summary>
    /// Builds the menu <paramref name="user"/> can see: top-level items with their children, each level
    /// sorted by <see cref="NavNode.Order"/>, then alphabetically by <see cref="NavNode.Label"/>.
    /// </summary>
    /// <param name="user">The user to build the menu for.</param>
    /// <param name="authorization">
    /// Used to check each node's <see cref="NavNode.Policy"/>. Passed in rather than injected because
    /// this service is a singleton and authorization handlers may be scoped; inject it into the
    /// calling view or component.
    /// </param>
    /// <param name="location">
    /// Where the user is, used to set <see cref="NavMenuItem.State"/> (see <see cref="NavLocation"/> for the
    /// rules). Usually <c>NavLocation.From(HttpContext)</c>. <c>null</c> leaves every item <see cref="NavItemState.None"/>.
    /// </param>
    /// <returns>The visible top-level items.</returns>
    /// <remarks>
    /// <para>
    /// A node is left out, along with everything under it, when the user fails its policy. A submenu
    /// is also left out when none of its children are visible.
    /// </para>
    /// <para>
    /// Hiding is cosmetic. The page or controller behind each target must still enforce access.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">A node names a policy that isn't registered.</exception>
    Task<IReadOnlyList<NavMenuItem>> GetMenuAsync(
        ClaimsPrincipal user, IAuthorizationService authorization, NavLocation? location = null);
}
