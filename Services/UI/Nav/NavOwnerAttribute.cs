// OpenOrderSystem.Core/Services/UI/Nav/NavOwnerAttribute.cs
namespace OpenOrderSystem.Core.Services.UI.Nav;

/// <summary>
/// Declares which nav item a page or controller action belongs to, so that item is marked active
/// while the page is open even though the page isn't in the menu itself.
/// </summary>
/// <remarks>
/// <para>
/// Put it on a Razor <c>PageModel</c>, a controller, or a single action. A declared owner takes
/// priority over the folder and controller matching described on <see cref="NavLocation"/>.
/// </para>
/// <para>
/// If the key names a submenu, that submenu is marked <see cref="NavItemState.ContainsActive"/> and no
/// item is active. If the key isn't registered, a warning is logged; if it is registered but hidden from
/// the user, it's ignored silently. Either way, normal matching is used instead.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [NavOwner("catalog:products")]
/// public class BulkPriceEditModel : PageModel { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class NavOwnerAttribute(string key) : Attribute
{
    /// <summary>The <see cref="NavNode.Key"/> of the owning nav item.</summary>
    public string Key { get; } = key;
}
