# Catalog DTOs

`Services/Catalog/Dto/`. All are immutable `record`s with nullable fields, so one shape works both
as a full read result and as a partial write payload. Use `with { … }` to modify one. Whether `null`
means "unchanged" or "clear" depends on the method (see
[ICatalogManager.md](ICatalogManager.md#update-semantics-at-a-glance)).

## `CatalogProductDto`

| Field | Read | Write |
|---|---|---|
| `Id` | Product ID | Required for update and delete; ignored on add |
| `Name`, `Description`, `Keywords` | Always populated | Null = unchanged (update). `Name` is required on add |
| `Metadata` | With `IncludeMetadata` | Keys are merged into the existing metadata |
| `Variants` | With `IncludeVariants` | Full-set sync ([rules](Product-Variant-And-Option-Sync.md)) |
| `Options` | With `IncludeOptions` | Full-set sync of option links ([rules](Product-Variant-And-Option-Sync.md)) |
| `CoverMedia` | With `IncludeMedia` | Only `.Id` is used |
| `MediaAlbum` | With `IncludeMedia` (a media group) | Only `.Id` is used |
| `CatalogProductGroup` | Currently always null ([known issue](README.md#known-issues)) | Only `.Id` is used |
| `CurrentSnapshot` | With `IncludeSnapshot` | Ignored |
| `IsArchived` | Always populated | Ignored. Use `ArchiveProduct` / `RestoreProduct` |
| `Flags` | Always populated (`ProductFlags` bitmask, for backend code) | Ignored. Use `UpdateProductFlags` |
| `FlagStates` | Always populated: every named flag (all except `None`) as `name → bool`, e.g. `{ "OutOfStock": false, "Hidden": false, "IsPromoted": true, … }`. Intended for frontends, so they never decode the bitmask | Ignored |

## `CatalogVariantDto`

`Id, GroupId, ProductId, Name, Price, StrikePrice, Cost, Sku, Barcode, Metadata`

- `Price` is the selling price. `StrikePrice` is an optional "was" price for display. `Cost` is internal.
- `Barcode` is a `Variant.BarcodeRecord(BarcodeSymbology Symbology, string Code)`.
- `GroupId` refers to a variant group (used for bulk pricing).
- When written through `UpdateProduct`, each entry is **full state** ([rules](Product-Variant-And-Option-Sync.md#rule-3-each-entry-is-full-state)).

## `CatalogProductOptionDto`

The link between a product and an option.

| Field | Read | Write |
|---|---|---|
| `ProductId` | The product | Optional; must match the product if set |
| `OptionId` | The option | Required |
| `DefaultState` | `Optional` or `Included` | Null = `Optional` |
| `PriceDeltaOverride` | Per-product price, if any | Null = clear (use the option's global price) |
| `Option` | The full `CatalogOptionDto` | Ignored |
| `EffectivePriceDelta` | What this option actually costs on this product: `0` if `Included`, otherwise the override if set, otherwise the option's `PriceDelta` | Ignored |

## `CatalogOptionDto`

`Id, GroupId, Name, PriceDelta, Flags`. The option itself. `PriceDelta` is the global default price; products can override it through their links.

## `CatalogMediaDto`

`Id, GroupId, Name, Description, Filepath, OriginalFileName, Extension, MimeType, MediaType, SizeBytes, Hash, Metadata`.
These are metadata only; the service never touches files on disk.

## `CatalogGroupDto`

`Id, ParentId, Name, Description, SortPriority`. The same shape is used for product, option and media groups.

## `CatalogProductSnapshotDto`

`Id, ProductId, PreviousSnapshotId, SchemaVersion, SnapshotJson, SnapshotHash`. Read-only. See [Product-Snapshots.md](Product-Snapshots.md).
