# Icons

Semantic icon keys (`nav.orders`, `action.print`, ...) and the icon packs that supply markup for them.
Views ask for a key, not a specific icon, so the whole UI can be reskinned by swapping packs.

**Source:** `Services/UI/Theme/Icons/`

## Topics

| File | Covers |
|---|---|
| [icon-keys.html](icon-keys.html) | Generated preview of every key in the contract, drawn with the Default Pack |

## Architecture

| Type | Role |
|---|---|
| `IIconProvider` | A source of icon markup. Returns `null` to pass a key on to the next provider, `""` to show no icon, or HTML. |
| `IconKeys` | The key contract as string constants (`IconKeys.Nav.Orders`), plus `IconKeys.All`. Generated. |
| `DefaultIconPack` | The pack that ships with OOS (`Name` = `"Default"`). Lucide icons, covers every key in `IconKeys`. |
| `DefaultPackIconMap` | Internal key → sprite symbol map, in `DefaultIconPack.cs` so the pack is one file. Edited by hand. |
| `FontAwesomeSlabIconPack` | Font Awesome 7 Slab Regular (`Name` = `"FontAwesomeSlab"`). Ships no files; needs a Pro+ kit on the page. Covers 156 of 194 keys. Not registered. |

## The Default Pack

- **Markup.** `ResolveIcon` returns an inline `<svg class="oos-icon">` that `<use>`s a symbol from
  `wwwroot/icons/default/sprite.svg`. Colour comes from `currentColor`, so icons follow the
  surrounding text. Size comes from `.oos-icon` in `wwwroot/css/oos-icons.css`, which
  `_HtmlBaseLayout.cshtml` links: 1.25em square, so icons scale with the font size.
- **Coverage.** Every contract key resolves. Unknown keys and namespaced plugin keys
  (`pluginid:name`) return `null`.
- **Caching.** The sprite and thumbnail URLs carry `?v=lucide-<version>`, so a regenerated set
  replaces what browsers have cached.
- **Thumbnail.** `wwwroot/icons/default/thumbnail.svg` is a grid of the icons named in
  `thumbnailKeys` in `icon-map.json`. Its colours are fixed because the image is shown through `<img>`,
  which doesn't pass `currentColor` in from the page.
- **License.** Lucide is ISC licensed. `wwwroot/icons/default/LICENSE-lucide.txt` must ship with the sprite.

## The Font Awesome Slab pack

- **Markup.** `ResolveIcon` returns `<i class="fa-slab fa-regular fa-<name>" aria-hidden="true"></i>`. Font
  Awesome styles it, so it inherits colour and font size like any Font Awesome icon. It does not use `.oos-icon`.
- **Requirements.** Slab is a Pro+ family added in Font Awesome 7. The page must load a Font Awesome 7 kit
  licensed for it. The Font Awesome 6.5 free fallback in `_HtmlBaseLayout.cshtml` doesn't include Slab, and
  these icons render blank under it.
- **Coverage.** Slab is a curated set, so 38 keys have no suitable icon and return `null`. They fall through
  to the next provider (normally the Default Pack) and draw in Lucide's style. They are: `nav.print-bridges`,
  `nav.scanners`, `nav.plugins`, `nav.system`, `action.save`, `action.import`, `action.test`, `action.power`,
  `action.more`, `action.more-vertical`, `status.loading`, `status.offline`, `status.connected`,
  `status.disconnected`, `status.disabled`, `status.enabled`, `status.verified`, `order.stage`, `order.tip`,
  `order.receipt`, `order.note`, `order.allergen`, `order.kitchen`, `order.size`, `order.quantity`,
  `ui.sidebar`, `ui.drag`, `pool.fuel`, `pool.basket`, `pool.wallet`, `pool.chart-line`, `pool.trending-up`,
  `pool.megaphone`, `pool.handshake`, `pool.qr-code`, `pool.barcode`, `pool.pizza`, `pool.sandwich`.
- **Verification.** Every icon name in the map was checked against the Font Awesome 7.3.1 metadata API
  (`api.fontawesome.com`) for Slab Regular. There is no build step for this pack, so recheck by hand when
  upgrading Font Awesome.
- **Thumbnail.** An inline `data:` SVG card with text only. The Pro+ glyphs can't be bundled, and `<img>`
  can't use the page's kit.

## Changing keys or icons

Everything generated comes from `scripts/icons/icon-map.json`. Edit it, then from `OpenOrderSystem-Core` run:

```
python3 scripts/icons/build-icons.py
```

The script downloads the pinned `lucide-static` version from the npm registry (checked against the
registry's sha512 integrity hash; npm itself isn't needed). To build offline, pass an extracted Lucide
`icons` folder as the first argument. It rewrites the sprite, thumbnail, license, `IconKeys.g.cs` and
`icon-keys.html`. Don't edit those files by hand.

The key map (`DefaultPackIconMap`) is not generated, so each pack stays a single `.cs` file. After
regenerating, the script checks the map in `DefaultIconPack.cs` against `icon-map.json`. If they differ, it
lists the lines to add, change or remove and exits with an error. Update the map, then rerun the script
until the check passes.

Adding a key to a category adds it to the contract. Other packs then report it as missing, and the
Default Pack fills it in until they add it.

## Registration

`RunOOS.cs` registers `DefaultIconPack` as a singleton `IIconProvider`.

## Known issues

- **No resolver yet.** Nothing walks the priority ladder or calls `ResolveIcon` from views, so the
  Default Pack is registered but not used anywhere in the UI.
