# Catalog Service

The catalog owns everything a customer can order: products, their variants, options and how options
attach to products, media, the groups that organize all of these, and product snapshots.

**Source:** `Services/Catalog/`  
**Models:** `Data/DataModels/V2/Catalog/`

## Topics

| File | Covers |
|---|---|
| [IReadOnlyCatalog.md](IReadOnlyCatalog.md) | All read/lookup methods |
| [ICatalogManager.md](ICatalogManager.md) | All create/update/delete methods |
| [Product-Variant-And-Option-Sync.md](Product-Variant-And-Option-Sync.md) | How `AddProduct`/`UpdateProduct` handle variants and option links |
| [Product-Snapshots.md](Product-Snapshots.md) | What snapshots are, when they are created, how ordering uses them |
| [Results-Flags-And-Enums.md](Results-Flags-And-Enums.md) | `CatalogResult` and its statuses, query result types, `ProductLocatorFlags`, `CatalogGroupType` |
| [DTOs.md](DTOs.md) | Every catalog DTO and its field semantics |

## Interfaces

| Type | Role |
|---|---|
| `IReadOnlyCatalog` | Read-only surface. Inject this anywhere that only needs lookups (e.g. the cart service). |
| `ICatalogManager` | Extends `IReadOnlyCatalog` with mutations. Inject only where writes are needed (admin tooling). |
| `CatalogService` | The single implementation of both. |
| `ICatalogDataContext` | The catalog `DbSet`s. `OosDbContext` implements it. |

## How it works

- **A context per call.** `CatalogService` takes `IDbContextFactory<OosDbContext>` and creates and
  disposes a context in every method. It holds no state, so it is safe to register as scoped or singleton.
- **It never throws.** Every method catches exceptions and returns a result with
  `Status = StorageError` and the exception message. Always check `IsSuccess` or `Status`
  (see [Results-Flags-And-Enums.md](Results-Flags-And-Enums.md)).
- **DTOs in, DTOs out.** Callers never see EF entities. All DTO fields are nullable so one record shape
  serves both as a full read result and as a partial update payload.
- **Products are archived, never deleted.** Archived products are hidden from every read unless
  `ProductLocatorFlags.IncludeArchived` is set (see [ICatalogManager.md](ICatalogManager.md#archiving-products)).
- **Snapshots are catalog-owned.** Product snapshots are written as a side effect of product
  mutations; no other service creates them (see [Product-Snapshots.md](Product-Snapshots.md)).

## Entity overview

| Entity | Notes |
|---|---|
| `Product` | Name, description, keywords, metadata, cover media, media album, product group, `Flags`, `IsArchived` (indexed). |
| `Variant` | Belongs to exactly one product. Holds price, strike price, cost, SKU (unique when set), barcode, metadata, optional variant group. |
| `Option` | Independent of products. Holds a global `PriceDelta` and `OptionFlags`. |
| `ProductOption` | Join between a product and an option: `DefaultState` (Optional/Included) and an optional `PriceDeltaOverride`. Composite key `(ProductId, OptionId)`. |
| `Media` | File metadata (path, MIME type, size, hash, metadata). |
| `ProductGroup` / `OptionGroup` / `MediaGroup` / `VariantGroup` | Tree-shaped groups (`ParentId`, `SortPriority`), all implementing `IGroupable`. Membership is optional: a member's `GroupId` may be null. Variant groups can span products, which is what bulk pricing uses. |
| `ProductSnapshot` | Immutable JSON capture of a product graph, chained by `PreviousSnapshotId`. |

## Registration status

**Not registered yet, deliberately.** V2 services will be registered together through a single extension
method once the end-to-end ordering flow is complete. The registrations the catalog needs:

```csharp
services.AddScoped<CatalogService>();
services.AddScoped<ICatalogManager>(sp => sp.GetRequiredService<CatalogService>());
services.AddScoped<IReadOnlyCatalog>(sp => sp.GetRequiredService<CatalogService>());
```

## Known issues

- **`CatalogProductGroup` is always `null` on product reads.** `BuildProductQuery` never includes
  `Product.Group` and lazy loading is not enabled, so the group is never mapped. `GroupId` is stored correctly.
- **Variant groups cannot be managed.** `CatalogGroupType` has no `Variant` member, so variant groups
  cannot be created, updated, deleted or found through the service. Bulk pricing by variant group works.
- **Option and media changes do not re-snapshot.** See [Product-Snapshots.md](Product-Snapshots.md#when-snapshots-are-not-created).
- **`AddOption`, `AddMedia` and `AddGroup` return no ID.** The caller cannot learn the new identifier.
- **Partial updates cannot clear values.** In option, media, group and product-level updates, `null`
  means "unchanged", so nullable fields (e.g. a group's `ParentId`) can never be reset to null.
