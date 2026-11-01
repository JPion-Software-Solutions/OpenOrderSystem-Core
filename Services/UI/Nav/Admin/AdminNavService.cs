// OpenOrderSystem.Core/Services/UI/Nav/Admin/AdminNavService.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenOrderSystem.Core.Services.UI.Nav.Admin.Interface;

namespace OpenOrderSystem.Core.Services.UI.Nav.Admin;

/// <summary>
/// In-memory <see cref="IAdminNavService"/>. Registered as a singleton; safe to register and read
/// from multiple threads.
/// </summary>
public sealed class AdminNavService(ILogger<AdminNavService>? logger = null) : IAdminNavService
{
    private readonly ILogger _logger = logger ?? NullLogger<AdminNavService>.Instance;

    private static readonly IComparer<NavNode> SiblingOrder = Comparer<NavNode>.Create((a, b) =>
    {
        var result = a.Order.CompareTo(b.Order);
        if (result == 0) result = StringComparer.CurrentCultureIgnoreCase.Compare(a.Label, b.Label);
        if (result == 0) result = StringComparer.Ordinal.Compare(a.Key, b.Key); // keeps ties stable
        return result;
    });

    private readonly object _lock = new();
    private readonly List<NavNode> _nodes = [];
    private readonly Dictionary<string, NavNode> _byKey = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyCollection<NavNode> NavNodes
    {
        get
        {
            lock (_lock) return _nodes.ToArray();
        }
    }

    /// <inheritdoc />
    public void RegisterNode(NavNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (string.IsNullOrWhiteSpace(node.Key))
            throw new ArgumentException("A nav node must have a key.", nameof(node));

        lock (_lock)
        {
            if (_byKey.ContainsKey(node.Key))
                throw new InvalidOperationException($"A nav node with key '{node.Key}' is already registered.");

            if (node.ParentKey is not null)
            {
                if (!_byKey.TryGetValue(node.ParentKey, out var parent))
                    throw new InvalidOperationException(
                        $"Nav node '{node.Key}' names parent '{node.ParentKey}', which isn't registered. Register parents before their children.");
                if (parent.Target is not null)
                    throw new InvalidOperationException(
                        $"Nav node '{node.Key}' names parent '{node.ParentKey}', which is a link, not a submenu. Only nodes with no target can have children.");
            }

            _nodes.Add(node);
            _byKey.Add(node.Key, node);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NavMenuItem>> GetMenuAsync(
        ClaimsPrincipal user, IAuthorizationService authorization, NavLocation? location = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(authorization);

        // Build from a snapshot so registration can continue while the menu is built.
        var snapshot = NavNodes;
        var children = snapshot.ToLookup(node => node.ParentKey ?? string.Empty);
        var menu = await BuildLevelAsync(string.Empty);
        if (location is null) return menu;

        return NavActiveState.Apply(menu, location, ownerKey =>
        {
            if (!snapshot.Any(node => node.Key == ownerKey))
                _logger.LogWarning(
                    "{Endpoint} declares nav owner '{OwnerKey}', which isn't registered in the admin menu.",
                    location.Page ?? $"{location.Controller}.{location.Action}", ownerKey);
        });

        async Task<IReadOnlyList<NavMenuItem>> BuildLevelAsync(string parentKey)
        {
            var items = new List<NavMenuItem>();
            foreach (var node in children[parentKey].Order(SiblingOrder))
            {
                if (!await CanSeeAsync(node)) continue;

                if (node.Target is not null)
                {
                    items.Add(new NavMenuItem(node, []));
                    continue;
                }

                var visibleChildren = await BuildLevelAsync(node.Key);
                if (visibleChildren.Count > 0)
                    items.Add(new NavMenuItem(node, visibleChildren));
            }
            return items;
        }

        async Task<bool> CanSeeAsync(NavNode node) =>
            string.IsNullOrEmpty(node.Policy) || (await authorization.AuthorizeAsync(user, node.Policy)).Succeeded;
    }
}
