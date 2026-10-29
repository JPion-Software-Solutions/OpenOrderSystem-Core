# ICatalogManager — Writes

`Services/Catalog/Interfaces/ICatalogManager.cs`, implemented by `CatalogService`. Extends
[IReadOnlyCatalog](IReadOnlyCatalog.md).

Every method validates its input, opens a context, saves once, and returns a result. Nothing throws:
database failures come back as `StorageError` with the exception message.

## Update semantics at a glance

| Target | `null` field means | Can clear a value? |
|---|---|---|
| Product's own fields | Leave unchanged | No |
| Product `Flags` | Not changed by `UpdateProduct`; use `UpdateProductFlags` | Yes, via the `clear` mask |
| `Variants` / `Options` collections on a product | Leave the whole collection untouched | Yes: a supplied list is the full set, and each entry is full state. See [Product-Variant-And-Option-Sync.md](Product-Variant-And-Option-Sync.md) |
| Options, media, groups | Leave unchanged | No |

## Products

### `AddProduct(CatalogProductDto product)` → `CatalogProductQueryResult`
- `Name` is required (`ValidationError`). `Id` is ignored; a new ID is generated.
- Sets name, description, keywords, cover media, album, product group and metadata from the DTO.
- If `Variants` or `Options` are supplied, they are created in the same save, following the
  [sync rules](Product-Variant-And-Option-Sync.md). A variant entry with an `Id` is rejected.
- Creates the product's first [snapshot](Product-Snapshots.md).
- Returns the new product with `Variants` and `Options` populated (`WithFlags = IncludeVariants | IncludeOptions`).

### `UpdateProduct(CatalogProductDto product)` → `CatalogProductQueryResult`
- `Id` is required (`ValidationError`); `NotFound` if no such product.
- **Product fields are partial updates:** null means unchanged. `Metadata` keys are merged into the
  existing metadata; keys you don't send are kept.
- `CoverMedia`, `MediaAlbum` and `CatalogProductGroup` are linked by their `.Id`. The other fields on those DTOs are ignored.
- **`Variants` and `Options` use full-set sync** when supplied. Read [Product-Variant-And-Option-Sync.md](Product-Variant-And-Option-Sync.md) before calling this.
- Everything is validated before any change is applied, and saved in one transaction with one snapshot
  (no snapshot is added if the product graph is unchanged).
- Returns the product with `Variants` and `Options` populated.

- Works on archived products too, so an archived product can be corrected before it is restored.

### `UpdateProductFlags(Guid productId, ProductFlags set = None, ProductFlags clear = None)` → `CatalogProductQueryResult`

Product flags (`OutOfStock`, `Hidden`, `IsHighlighted`, `IsPromoted`, operator `Custom1–6`, `Addon1–6`)
are changed **only** here. `UpdateProduct` never touches them.

- Turns on the bits in `set` and turns off the bits in `clear`; every other bit is left as it is. This way
  an operator toggling `Hidden` and the promotion service setting `IsPromoted` can't overwrite each other.
- A bit in both masks is a `ValidationError`. `NotFound` if the product doesn't exist.
- Works on archived products. If nothing changes, nothing is saved.
- **No snapshot is written.** Flags are operational state, not product content.
- Returns the product's scalar fields, including the resulting `Flags` and `FlagStates`.

```csharp
await catalog.UpdateProductFlags(id, set: ProductFlags.OutOfStock);                  // mark unavailable
await catalog.UpdateProductFlags(id, clear: ProductFlags.OutOfStock | ProductFlags.Hidden);
```

**Concurrency:** this loads the row, changes it and saves it. Two calls on the same product at the same
moment can still race, and the last save wins for the whole value.

### Archiving products

Products are **never deleted**. Deleting one would remove its snapshots, which order lines depend on.
Instead a product is archived: it stays in the database with its variants, option links and snapshots,
and is hidden from reads.

| Method | Behavior |
|---|---|
| `ArchiveProduct(CatalogProductDto)` / `ArchiveProduct(Guid)` | Sets `IsArchived`. The DTO overload requires `Id`. Archiving an already-archived product succeeds without change. No snapshot is written. |
| `RestoreProduct(CatalogProductDto)` / `RestoreProduct(Guid)` | Clears `IsArchived`. Restoring a product that isn't archived succeeds without change. |

Both return the product's scalar fields, including `IsArchived`.

**What archiving affects:**
- Every product read (`FindProduct`, `FindProducts`, `EnrichProduct`) treats it as not found unless
  `IncludeArchived` is set. `FindVariant` doesn't return its variants.
- The cart can't add it (`AddItemAsync` returns `NotFound`). Lines already in a cart keep their frozen data.
- Its variants **keep their SKUs**, so those SKUs can't be reused by another product.
- `UpdateVariantGroupPricing` still reprices its variants and re-snapshots it.

**Safety net:** the `ProductSnapshot → Product` and `OrderLine → ProductSnapshot` foreign keys are
`Restrict`, so a hard delete issued outside the service fails instead of cascading into order history.

## Options

Options exist independently of products. Linking an option to a product (with defaults or a price
override) is done through `UpdateProduct`'s `Options` collection, not here.

| Method | Behavior |
|---|---|
| `AddOption(CatalogOptionDto)` | `Name` is required. `PriceDelta` defaults to `0`, `Flags` to `None`. **Doesn't return the new ID.** |
| `UpdateOption(CatalogOptionDto)` | `Id` is required. Partial update of name, price delta, flags and group. **Doesn't re-snapshot products that use the option.** |
| `DeleteOption(CatalogOptionDto)` / `DeleteOption(Guid)` | Hard delete. Option links to products are removed by cascade, and **products aren't re-snapshotted**. |

## Media

| Method | Behavior |
|---|---|
| `AddMedia(CatalogMediaDto)` | `Name` and `Filepath` are required. Missing `Extension`, `MimeType` and `Hash` default to empty, `MediaType` to `Unsupported`, `SizeBytes` to `0`. **Doesn't return the new ID.** Only records metadata; it does not upload or move files. |
| `UpdateMedia(CatalogMediaDto)` | `Id` is required. Partial update; `Metadata` keys are merged. Sets `UpdatedUtc`. |
| `DeleteMedia(CatalogMediaDto)` / `DeleteMedia(Guid)` | Removes the record only, not the file on disk. If a product uses it as cover media, the database rejects the delete (restrict FK) and you get `StorageError`. |

## Groups

`groupType` picks the table: `Product`, `Option` or `Media`. Variant groups can't be managed yet.

| Method | Behavior |
|---|---|
| `AddGroup(CatalogGroupDto, groupType)` | `Name` is required. `Description` defaults to empty, `SortPriority` to `0`. **Doesn't return the new ID.** |
| `UpdateGroup(CatalogGroupDto, groupType)` | `Id` is required. Partial update of name, description, parent and sort priority. A group cannot be moved back to the root because a null `ParentId` means unchanged. |
| `DeleteGroup(CatalogGroupDto, groupType)` / `DeleteGroup(Guid, groupType)` | Hard delete. If the group still has members or child groups, the database is expected to reject the delete (optional FKs with no cascade) and you get `StorageError`. Empty the group first. |

## Variant pricing (bulk)

| Method | Behavior |
|---|---|
| `UpdateVariantGroupPricing(Guid variantGroupId, price, strikePrice)` | Sets `Price` and/or `StrikePrice` on every variant in the group, across all products. Each affected product gets a new snapshot. Passing both as null is a successful no-op. |
| `UpdateVariantGroupPricing(string variantGroupName, price, strikePrice)` | Same, for every variant group with that name. `NotFound` if no group matches. |

A strike price can't be cleared this way, because null means unchanged. To clear one variant's strike
price, use `UpdateProduct` with the full variant list.

Variants are added, updated and deleted individually through `UpdateProduct`; there are no separate variant methods.
