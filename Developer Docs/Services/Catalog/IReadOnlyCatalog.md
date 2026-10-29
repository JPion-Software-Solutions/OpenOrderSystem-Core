# IReadOnlyCatalog — Reads

`Services/Catalog/Interfaces/IReadOnlyCatalog.cs`, implemented by `CatalogService`.

Inject `IReadOnlyCatalog` wherever you only need lookups. Every method returns a result object and
never throws (see [Results-Flags-And-Enums.md](Results-Flags-And-Enums.md)).

## Conventions

- **Archived products are invisible by default.** Every product read excludes archived products unless
  `ProductLocatorFlags.IncludeArchived` is set; without it they behave as if they don't exist.
  `FindVariant` never returns variants of archived products.

- **Single-item finders** (`FindProduct`, `FindVariant`, `FindOption`, `FindMedia`, `FindGroup`)
  return `Status = NotFound` (so `IsSuccess == false`) and empty `Results` when nothing matches.
  When something matches, `Results` has exactly one entry; unwrap with `.SingleOrDefault()`.
- **List finders** (`FindProducts`, `FindOptions`, `FindMediaInGroup`, `FindGroups`) return
  `Success` with an empty `Results` list when nothing matches. An empty result is not an error.
- **Name lookups are exact matches.** The single-item `string` overloads return the *first* match
  if several entities share a name, and the order is not defined.
- **Group-name lookups match every group with that name**, then return the members of all of them.

## Products

All product reads take `ProductLocatorFlags` (default `None`) to choose which related data is
loaded. Without flags you get only the product's own scalar fields. `CatalogProductGroup` is currently
always `null` on reads (see the [catalog README](README.md#known-issues)).
`WithFlags` on the result echoes the flags that were used.

| Method | Behavior |
|---|---|
| `FindProduct(Guid id, flags)` | Looks up a product by ID. |
| `FindProduct(string name, flags)` | Looks up a product by exact name, returning the first match. |
| `FindProducts(Guid groupId, flags)` | Returns all products in the group. Add `CollapseChildrenGroupMembers` to include every descendant group, and/or `CollapseParentGroupMembers` to include ancestor groups. |
| `FindProducts(string groupName, flags)` | Same as above, applied to every product group with that name, with results combined. Returns an empty success result if no group has that name. |
| `EnrichProduct(CatalogProductDto product, flags)` | Re-fetches the product by `product.Id` with the requested flags. Only `Id` is read from the input; every other field is ignored. `ValidationError` if `Id` is null. |

**What each flag adds to `CatalogProductDto`:**

| Flag | Populates |
|---|---|
| `IncludeVariants` | `Variants` |
| `IncludeOptions` | `Options`: the product's option links (`CatalogProductOptionDto`), each with the full `Option` and the resolved `EffectivePriceDelta` |
| `IncludeMedia` | `CoverMedia`, `MediaAlbum` |
| `IncludeMetadata` | `Metadata` |
| `IncludeSnapshot` | `CurrentSnapshot`, the most recent snapshot, including its `Id` |
| `IncludeArchived` | Doesn't add a field. Includes archived products in the results (`IsArchived` is always populated) |

> Ordering uses `FindProduct(id, IncludeSnapshot)` to get the snapshot ID to stamp onto `OrderLine`. See [Product-Snapshots.md](Product-Snapshots.md).

## Variants

| Method | Behavior |
|---|---|
| `FindVariant(Guid id)` | Looks up a single variant by ID. The result includes `ProductId` so you can find its parent product. Returns `NotFound` if the parent product is archived. |

There is no variant-by-name or variant-by-group lookup. To list a product's variants, use
`FindProduct(id, IncludeVariants)`.

## Options

These return the options themselves (global `PriceDelta`), **not** product-specific pricing. For
the price an option has on a particular product, read the product with `IncludeOptions` and use
`EffectivePriceDelta`.

| Method | Behavior |
|---|---|
| `FindOption(Guid id)` | Looks up an option by ID. |
| `FindOption(string name)` | Looks up an option by exact name, returning the first match. |
| `FindOptions(Guid groupId)` | Returns the options directly in that group. Child groups are **not** included. |
| `FindOptions(string groupName)` | Returns the options in every option group with that name. Child groups are not included. |

## Media

| Method | Behavior |
|---|---|
| `FindMedia(Guid id)` | Looks up a media record by ID. |
| `FindMedia(string name)` | Looks up a media record by exact name, returning the first match. |
| `FindMediaInGroup(Guid groupId)` | Returns the media directly in that group. Child groups are not included. |
| `FindMediaInGroup(string groupName)` | Returns the media in every media group with that name. |

## Groups

`groupType` selects which group table is searched: `Product`, `Option` or `Media`. Variant groups
can't be looked up here yet (see the [catalog README](README.md#known-issues)). `GroupType` on the
result echoes the type you asked for.

| Method | Behavior |
|---|---|
| `FindGroup(Guid id, groupType)` | Looks up a single group by ID. |
| `FindGroups(string name, groupType)` | Returns every group of that type with the exact name. |
