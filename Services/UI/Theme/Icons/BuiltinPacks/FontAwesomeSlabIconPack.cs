// OpenOrderSystem.Core/Services/UI/Theme/Icons/BuiltinPacks/FontAwesomeSlabIconPack.cs

using Microsoft.AspNetCore.Html;
using OpenOrderSystem.Core.Services.UI.Theme.Icons.Interface;

namespace OpenOrderSystem.Core.Services.UI.Theme.Icons.BuiltinPacks;

/// <summary>
/// An icon pack using the Font Awesome 7 <b>Slab</b> family (Regular style), rendered as
/// <c>&lt;i class="fa-slab fa-regular fa-…"&gt;</c> elements.
/// </summary>
/// <remarks>
/// <para>
/// Ships no icon files. Slab is a Font Awesome Pro+ family, so the page must load a Font Awesome 7
/// kit or stylesheet licensed for it; without one these icons render blank.
/// </para>
/// <para>
/// Slab is a curated set of a few hundred icons, so this pack covers only part of
/// <see cref="Icons.IconKeys"/>. Keys missing from <see cref="FontAwesomeSlabIconMap"/> return <c>null</c>
/// and fall through to a lower provider (normally <see cref="DefaultIconPack"/>), which draws them in
/// a different style. Namespaced plugin keys (<c>pluginid:name</c>) are passed on as well.
/// </para>
/// <para>
/// The map was checked against the Font Awesome 7.3.1 metadata API: every icon name it uses
/// exists in Slab Regular. Recheck it when moving to a new Font Awesome version.
/// </para>
/// </remarks>
public sealed class FontAwesomeSlabIconPack : IIconProvider
{
    /// <summary>The value of <see cref="Name"/>. Stored in the priority ladder, so it must not change.</summary>
    public const string ProviderName = "FontAwesomeSlab";

    private readonly IReadOnlyDictionary<string, string> _markup;

    public FontAwesomeSlabIconPack()
    {
        _markup = FontAwesomeSlabIconMap.Names.ToDictionary(
            pair => pair.Key,
            pair => BuildMarkup(pair.Value),
            StringComparer.Ordinal);
        IconKeys = [.. FontAwesomeSlabIconMap.Names.Keys];
    }

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public string Description =>
        "Font Awesome Slab (Regular). Requires a Font Awesome 7 Pro+ kit. " +
        "Covers most icon keys; the rest come from the next pack down.";

    /// <inheritdoc />
    /// <remarks>
    /// An inline placeholder card rather than icon artwork: the Slab glyphs are Pro+ licensed and
    /// can't be bundled, and an <c>&lt;img&gt;</c> can't use the page's Font Awesome kit.
    /// </remarks>
    public string ThumbnailUrl => Thumbnail;

    /// <inheritdoc />
    public string[] IconKeys { get; }

    /// <inheritdoc />
    public HtmlString? ResolveIcon(string key) =>
        _markup.TryGetValue(key, out var markup) ? new HtmlString(markup) : null;

    /// <summary>
    /// Colour and size are inherited from the surrounding text, as with any Font Awesome icon.
    /// The kit's SVG+JS mode swaps the <c>&lt;i&gt;</c> for an inline <c>&lt;svg&gt;</c> and keeps these classes.
    /// </summary>
    private static string BuildMarkup(string faName) =>
        $"<i class=\"fa-slab fa-regular fa-{faName}\" aria-hidden=\"true\"></i>";

    private const string Thumbnail =
        "data:image/svg+xml;charset=utf-8," +
        "%3Csvg xmlns='http://www.w3.org/2000/svg' width='144' height='104' viewBox='0 0 144 104'%3E" +
        "%3Crect width='144' height='104' rx='12' fill='%23fafaf9' stroke='%23e7e5e4'/%3E" +
        "%3Ctext x='72' y='50' text-anchor='middle' font-family='Georgia,serif' font-size='22' font-weight='700' fill='%231c1917'%3ESlab%3C/text%3E" +
        "%3Ctext x='72' y='72' text-anchor='middle' font-family='system-ui,sans-serif' font-size='11' fill='%2378716c'%3EFont Awesome Pro+%3C/text%3E" +
        "%3C/svg%3E";
}

/// <summary>
/// Icon key to Font Awesome icon name (without the <c>fa-</c> prefix), for the Slab Regular style.
/// Keys not listed here have no suitable Slab icon and fall through to the next provider.
/// </summary>
internal static class FontAwesomeSlabIconMap
{
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        ["nav.dashboard"] = "gauge",
        ["nav.home"] = "house",
        ["nav.orders"] = "clipboard",
        ["nav.order-terminal"] = "desktop",
        ["nav.catalog"] = "book-open",
        ["nav.products"] = "utensils",
        ["nav.variants"] = "layer-group",
        ["nav.options"] = "sliders",
        ["nav.product-groups"] = "folder",
        ["nav.media"] = "image",
        ["nav.promotions"] = "percent",
        ["nav.customers"] = "address-card",
        ["nav.users"] = "users",
        ["nav.roles"] = "shield",
        ["nav.security"] = "shield",
        ["nav.keyring"] = "key",
        ["nav.devices"] = "mobile",
        ["nav.printers"] = "print",
        ["nav.terminals"] = "laptop",
        ["nav.reports"] = "chart-bar",
        ["nav.logs"] = "list",
        ["nav.integrations"] = "link",
        ["nav.policies"] = "file",
        ["nav.email"] = "envelope",
        ["nav.sms"] = "comment",
        ["nav.notifications"] = "bell",
        ["nav.schedule"] = "calendar",
        ["nav.hours"] = "clock",
        ["nav.settings"] = "gear",
        ["nav.configuration"] = "sliders",
        ["nav.maintenance"] = "wrench",
        ["nav.database"] = "database",
        ["nav.backups"] = "box-archive",
        ["nav.help"] = "circle-question",
        ["nav.docs"] = "book",
        ["nav.profile"] = "circle-user",
        ["nav.login"] = "arrow-right-to-bracket",
        ["nav.logout"] = "arrow-right-from-bracket",

        ["action.add"] = "plus",
        ["action.edit"] = "pencil",
        ["action.delete"] = "trash",
        ["action.cancel"] = "xmark",
        ["action.confirm"] = "check",
        ["action.search"] = "magnifying-glass",
        ["action.filter"] = "filter",
        ["action.sort"] = "sort",
        ["action.refresh"] = "arrows-rotate",
        ["action.sync"] = "arrows-rotate",
        ["action.print"] = "print",
        ["action.reprint"] = "print",
        ["action.hold"] = "pause",
        ["action.resume"] = "play",
        ["action.advance"] = "forward",
        ["action.undo"] = "arrow-rotate-left",
        ["action.redo"] = "arrow-rotate-right",
        ["action.copy"] = "copy",
        ["action.duplicate"] = "copy",
        ["action.download"] = "arrow-down-to-line",
        ["action.upload"] = "arrow-up-from-line",
        ["action.export"] = "arrow-up-from-bracket",
        ["action.archive"] = "box-archive",
        ["action.restore"] = "arrow-rotate-left",
        ["action.view"] = "eye",
        ["action.hide"] = "eye-slash",
        ["action.lock"] = "lock",
        ["action.unlock"] = "lock-open",
        ["action.link"] = "link",
        ["action.share"] = "share-nodes",
        ["action.send"] = "paper-plane",
        ["action.reset"] = "arrow-rotate-left",

        ["status.success"] = "check",
        ["status.warning"] = "triangle-exclamation",
        ["status.error"] = "xmark",
        ["status.info"] = "circle-info",
        ["status.online"] = "wifi",
        ["status.pending"] = "hourglass",
        ["status.complete"] = "check",
        ["status.in-progress"] = "circle-half-stroke",
        ["status.new"] = "star",
        ["status.alert"] = "bell",
        ["status.unread"] = "circle",

        ["order.queue"] = "list",
        ["order.cart"] = "cart-shopping",
        ["order.checkout"] = "bag-shopping",
        ["order.pickup"] = "box",
        ["order.delivery"] = "truck",
        ["order.dine-in"] = "utensils",
        ["order.scheduled"] = "calendar",
        ["order.time-slot"] = "clock",
        ["order.payment"] = "credit-card",
        ["order.cash"] = "money-bill-wave",
        ["order.refund"] = "arrow-rotate-left",
        ["order.tax"] = "landmark",
        ["order.ticket"] = "ticket",
        ["order.customer"] = "user",
        ["order.phone-order"] = "phone",
        ["order.online-order"] = "globe",
        ["order.ready"] = "bell",
        ["order.timer"] = "stopwatch",

        ["ui.menu-toggle"] = "bars",
        ["ui.close"] = "xmark",
        ["ui.expand"] = "angle-down",
        ["ui.collapse"] = "angle-up",
        ["ui.next"] = "angle-right",
        ["ui.previous"] = "angle-left",
        ["ui.back"] = "arrow-left",
        ["ui.forward"] = "arrow-right",
        ["ui.move-up"] = "arrow-up",
        ["ui.move-down"] = "arrow-down",
        ["ui.external-link"] = "arrow-up-right-from-square",
        ["ui.fullscreen"] = "expand",
        ["ui.exit-fullscreen"] = "compress",
        ["ui.theme-light"] = "sun",
        ["ui.theme-dark"] = "moon",
        ["ui.theme-auto"] = "circle-half-stroke",

        ["pool.tag"] = "tag",
        ["pool.gift"] = "gift",
        ["pool.star"] = "star",
        ["pool.heart"] = "heart",
        ["pool.award"] = "trophy",
        ["pool.flag"] = "flag",
        ["pool.bookmark"] = "bookmark",
        ["pool.box"] = "box",
        ["pool.package"] = "box",
        ["pool.truck"] = "truck",
        ["pool.car"] = "car",
        ["pool.store"] = "shop",
        ["pool.warehouse"] = "building",
        ["pool.percent"] = "percent",
        ["pool.calendar"] = "calendar",
        ["pool.clock"] = "clock",
        ["pool.chart-bar"] = "chart-bar",
        ["pool.chart-pie"] = "chart-pie",
        ["pool.mail"] = "envelope",
        ["pool.phone"] = "phone",
        ["pool.message"] = "comment",
        ["pool.map-pin"] = "location-dot",
        ["pool.map"] = "map",
        ["pool.globe"] = "globe",
        ["pool.key"] = "key",
        ["pool.bell"] = "bell",
        ["pool.cloud"] = "cloud",
        ["pool.link"] = "link",
        ["pool.clipboard"] = "clipboard",
        ["pool.file"] = "file",
        ["pool.folder"] = "folder",
        ["pool.image"] = "image",
        ["pool.camera"] = "camera",
        ["pool.bolt"] = "bolt",
        ["pool.flame"] = "fire",
        ["pool.leaf"] = "leaf",
        ["pool.lightbulb"] = "lightbulb",
        ["pool.thumbs-up"] = "thumbs-up",
        ["pool.coffee"] = "mug-hot",
        ["pool.dessert"] = "cake-candles",
        ["pool.drink"] = "martini-glass",
    };
}
