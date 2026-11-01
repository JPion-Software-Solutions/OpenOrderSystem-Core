# Navigation

A data model for navigation menus, so menus can be built from data (including items that plugins add)
instead of hand-written view markup such as `Areas/Staff/Views/Shared/_AdminNavLinks.cshtml`.

**Source:** `Services/UI/Nav/`

> **Status:** the model and the admin registry (`IAdminNavService`) work, but nothing registers nodes or
> renders the menu yet. The existing admin nav is still `_AdminNavLinks.cshtml`.

## Architecture

| Type | Role |
|---|---|
| `NavNode` | One menu item: key, label, icon key, target, parent, sort order, policy. Immutable once built (`init` properties). |
| `NavTarget` | Abstract base for where an item goes. |
| `InternalRoute` | Abstract base for destinations inside OOS. Carries the area and optional route values. |
| `PageRoute` | A Razor Page, by page name (`asp-page`). |
| `ControllerRoute` | An MVC controller action, by controller and action. |
| `ExternalLink` | An absolute URL outside OOS. |
| `FormLink` | A POST to a handler path, with optional fixed form fields. |
| `IAdminNavService` | The admin menu registry: register nodes, build the menu for a user. |
| `AdminNavService` | In-memory, thread-safe implementation. Registered as a singleton in `RunOOS.cs`. |
| `NavMenuItem` | One item of a built menu: the node, its visible sorted children, and its `State` (`None`, `Active`, `ContainsActive`). |
| `NavLocation` | Where the user is (area, page or controller/action, declared owner). `NavLocation.From(HttpContext)` reads it from the request. |
| `NavOwnerAttribute` | Put on a page model, controller or action to name the nav item it belongs to. |
| `NavActiveState` | Internal. Applies the active-state rules to a built menu. |
| `NavMenuExtensions` | `GetBreadcrumbs()`: the trail from the top of a built menu to the current page. |

## How a menu is described

- **Tree.** Every node has a `Key`, unique within the menu. A node's `ParentKey` names the submenu it sits
  in. `null` means top level.
- **Submenus.** A node with `Target = null` is a submenu. It opens to show its children and doesn't
  navigate anywhere itself.
- **Order.** Siblings sort by `Order`, lowest first. Ties sort alphabetically by `Label`.
- **Icons.** `IconKey` is a semantic icon key (see [Icons](../Theme/Icons/README.md)), so the nav follows
  whichever icon pack is active. Empty means no icon.
- **Visibility.** `Policy` names an authorization policy. Items whose policy the user fails are left out
  of the menu. Empty means no check.

## Registering nodes

`IAdminNavService.RegisterNode` is the only way to add a node. `NavNodes` returns a read-only snapshot.
`RegisterNode` throws, and nothing is added, when:

| Problem | Exception |
|---|---|
| `node` is `null` | `ArgumentNullException` |
| `Key` is empty or whitespace | `ArgumentException` |
| A node with the same `Key` is already registered (keys are case-sensitive) | `InvalidOperationException` |
| `ParentKey` names a node that isn't registered yet | `InvalidOperationException` |
| The parent has a `Target`, so it's a link, not a submenu | `InvalidOperationException` |

**Parents must be registered before their children.** This also rules out cycles. Plugins should namespace
keys (`pluginid:name`) so they can't collide with core or each other.

## Building the menu

`GetMenuAsync(user, authorization, location)` returns the top-level `NavMenuItem`s the user can see:

- **Order.** Each level sorts by `Order`, then `Label` (current culture, ignoring case), then `Key` so the
  result is always the same.
- **Hiding.** A node whose `Policy` the user fails is left out, along with everything under it. A submenu
  with no visible children is also left out, so the menu never shows an empty dropdown.
- **Why `IAuthorizationService` is a parameter.** The service is a singleton, and authorization handlers
  can be scoped. Injecting it into the singleton would capture one scope for the app's lifetime. Inject it
  into the view or component that renders the menu and pass it in.
- **Unknown policies.** A `Policy` that isn't registered makes `AuthorizeAsync`, and so `GetMenuAsync`, throw
  `InvalidOperationException`. Registration can't catch this, because policies aren't known yet at that point.
- **Concurrency.** The menu is built from a snapshot, so nodes can keep registering while it's built.

## Active state

Pass a `NavLocation` to `GetMenuAsync`, and each `NavMenuItem` gets a `State`:

- **`Active`** is the link for the current page. At most one item per menu is active, and possibly none.
- **`ContainsActive`** is set on every submenu above the active item, at any depth. It's typically rendered
  expanded.
- **`None`** is everything else. Nothing below an item is affected, because only links can be active and
  links have no children.

How each state is styled, for example whether a collapsed sidebar highlights only the top-level submenu,
is up to the renderer. Without a `location`, every item is `None`.

```csharp
var menu = await nav.GetMenuAsync(User, authorization, NavLocation.From(HttpContext));
```

### Which item is active

Only items visible to the user are considered, so a link hidden by policy can't be active.

1. **A declared owner.** If the page, controller or action has `[NavOwner("key")]` and that item is visible,
   it wins, whatever the URL. If the owner is a submenu, that submenu and the ones above it are
   `ContainsActive` and no item is `Active`. This is for section pages such as a catalog overview.
2. **Otherwise, the most specific matching link:**

   | Link | Matches | Most specific first |
   |---|---|---|
   | `PageRoute` | Same area, and the same page or any page in the link's folder or below it | Exact page, then deeper folder, then the folder's `Index` page over other pages there |
   | `ControllerRoute` | Same area and controller | Exact action, then the `Index` action over other actions |
   | `ExternalLink`, `FormLink` | Never | |

   Remaining ties go to whichever item comes first in the menu. Route values (such as `?id=5`) are ignored,
   and names are compared case-insensitively.
3. **No match** leaves everything `None`.

Examples, all in the same area:

| Current page | Links in the menu | Active |
|---|---|---|
| `/Catalog/Products/Edit` | `/Catalog/Products/Index` | Products, by folder |
| `/Settings/Email/Smtp` | `/Settings/Index`, `/Settings/Email/Index` | Email, the deeper folder |
| `/Users/Edit` | `/Users/Create` ("Add user"), `/Users/Index` | Users, the folder's `Index` |
| `/Reports` | `/Index` (Dashboard) | None. Root pages only match exactly. |
| `/SettingsX/Foo` | `/Settings/Index` | None. Folders match on whole segments. |

### Pages outside the tree

A page that isn't in the menu, or that sits outside its nav item's folder, names its owner:

```csharp
[NavOwner("catalog:products")]
public class BulkPriceEditModel : PageModel { … }
```

The attribute is read from endpoint metadata by `NavLocation.From`, so it works the same on page models,
controllers and individual actions. If the key was never registered, a warning is logged (it's almost
certainly a typo) and normal matching is used instead. If the key is registered but hidden from this user,
matching falls back without a warning.

## Breadcrumbs

`menu.GetBreadcrumbs()` returns the trail from the top of a built menu down to the current page: each
`ContainsActive` submenu, then the `Active` item. For `/Settings/Email/Smtp` that's `Settings > Email`.

- **Empty** when nothing is active, or when the menu was built without a location.
- **Ends at a submenu** when the page's declared owner is a submenu.
- **Ends at the nav item, not the page.** On a page that isn't in the menu itself (matched by folder or
  declared with `[NavOwner]`), append the page's own title after the last crumb.

## Targets

Internal destinations come from routing rather than hard-coded paths, so they keep working when URLs,
areas or the app's base path change. Both kinds share `Area` (as in `asp-area`) and `RouteValues`, which
holds extra values such as an `id`.

- **`PageRoute(Page, Area?, RouteValues?)`** is a Razor Page. `Page` is the `asp-page` name: the path
  under the area's `Pages` folder, starting with `/` and without `.cshtml`, such as `/Users/Index`.
- **`ControllerRoute(Controller, Action, Area?, RouteValues?)`** is an MVC action, named as in
  `asp-controller` and `asp-action`.
- **`ExternalLink(Url)`** opens an absolute URL.
- **`FormLink(HandlerPath, FormData?)`** is for actions that must be a POST, such as logout. `FormData`
  is a fixed set of hidden fields chosen when the node is defined. The nav never takes input from the user.

## Gotchas

- **`Policy` is cosmetic.** It only hides links. Every controller or page must still enforce its own
  authorization, because a user can type the URL directly.
- **Record equality and dictionaries.** `PageRoute`, `ControllerRoute` and `FormLink` are records, but their dictionaries
  compare by reference. Two targets with identical route values or form data are not `==` unless they
  share the same dictionary instance.

## Known issues

- **No authorization policies are registered yet.** Access control is currently roles only
  (`[Authorize(Roles = "admin,manager")]`), so there is nothing for `Policy` to name. Policies are planned
  before the new admin panel ships.
- **Razor Pages aren't enabled.** `RunOOS.cs` calls `AddControllersWithViews` and `MapControllerRoute`, but not
  `AddRazorPages` or `MapRazorPages`, and the project has no `Pages` folders. A `PageRoute` won't resolve until
  Razor Pages are registered.
- **Nothing registers nodes or renders the menu.** No core nodes exist yet, and no view calls `GetMenuAsync`.
- **Rendering requirements not yet built.** When a renderer is written, a `FormLink` must include an
  antiforgery token, or ASP.NET Core rejects the POST.
