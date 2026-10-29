# Results, Flags & Enums

## `CatalogResult`

`Services/Catalog/CatalogResult.cs`. Every catalog method returns this type or a subclass of it.

| Member | Meaning |
|---|---|
| `Status` | A `CatalogResultStatus` |
| `Message` | Optional detail, usually set on failure (validation reason or exception message) |
| `IsSuccess` | `true` when `Status < 101` |

### `CatalogResultStatus`

Codes 1–100 are reserved for success states and 101 and up for errors.

| Value | Code | When |
|---|---|---|
| `Success` | 1 | The operation worked. A list query with no matches is still `Success`. |
| `NotFound` | 101 | A single-item lookup matched nothing, or the update/delete target doesn't exist |
| `ValidationError` | 102 | Bad input. `Message` says what's wrong |
| `Conflict` | 103 | Uniqueness violation detected by the service (e.g. a duplicate SKU) |
| `StorageError` | 104 | An exception was thrown (DB constraint, connectivity, …). `Message` holds the exception message |

## Query result types

Each adds a `Results` list. Single-item finders return a list of 0 or 1 entries.

| Type | Extra members |
|---|---|
| `CatalogProductQueryResult` | `Results: IReadOnlyList<CatalogProductDto>`, `WithFlags: ProductLocatorFlags` (the flags used) |
| `CatalogVariantQueryResult` | `Results: IReadOnlyList<CatalogVariantDto>` |
| `CatalogOptionQueryResult` | `Results: IReadOnlyList<CatalogOptionDto>` |
| `CatalogMediaQueryResult` | `Results: IReadOnlyList<CatalogMediaDto>` |
| `CatalogGroupQueryResult` | `Results: IReadOnlyList<CatalogGroupDto>`, `GroupType: CatalogGroupType` |

## `ProductLocatorFlags`

`Services/Catalog/ProductLocatorFlags.cs`. A `[Flags]` enum controlling what product reads load.
Combine values with `|`.

| Flag | Bit | Effect |
|---|---|---|
| `None` | 0 | Product scalar fields only |
| `IncludeOptions` | 1 << 0 | Load option links into `Options` |
| `IncludeVariants` | 1 << 1 | Load `Variants` |
| `IncludeMedia` | 1 << 2 | Load `CoverMedia` and `MediaAlbum` |
| `IncludeMetadata` | 1 << 3 | Include `Metadata` |
| `IncludeSnapshot` | 1 << 4 | Load the latest snapshot into `CurrentSnapshot` |
| `IncludeArchived` | 1 << 5 | Include archived products. Without it, they are treated as not found |
| `CollapseParentGroupMembers` | 1 << 10 | Group queries also return products in ancestor groups |
| `CollapseChildrenGroupMembers` | 1 << 11 | Group queries also return products in all descendant groups |

The collapse flags only affect `FindProducts`.

## `CatalogGroupType`

`Services/Catalog/CatalogGroupType.cs`. Chooses the group table for group methods.

| Value | Table |
|---|---|
| `Product` | `ProductGroups` |
| `Option` | `OptionGroups` |
| `Media` | `MediaGroups` |

There is no `Variant` value yet, so variant groups can't be managed through the group methods.

## Model enums you will see in DTOs

| Enum | Values |
|---|---|
| `ProductFlags` | `[Flags]`: `OutOfStock`, `Hidden`, `IsHighlighted`, `IsPromoted` (bits 0–3); operator-defined `Custom1–6` (bits 7–12); plugin-reserved `Addon1–6` (bits 15–20). Changed via `UpdateProductFlags` |
| `ProductOptionDefaultState` | `Optional` (customer may add it), `Included` (part of the product by default; effective delta is 0) |
| `OptionFlags` | `None`, `OutOfStock` (temporarily unavailable), `Hidden` (hidden from customers, visible to staff), `Disabled` |
| `MediaType` | Banded by category: `Image` 100–199, `Audio` 200–299, `Video` 300–399, `File` 400–499 (e.g. `Image_Png = 102`, `File_Pdf = 401`). The `*_STOP` values mark the end of each band. Unknown types are `Unsupported = 0`. Full list in `Data/DataModels/V2/Catalog/Media.cs` |
| `BarcodeSymbology` | `Unknown`, `UPC`, `Code128`, `EAN8`, `EAN13` |
