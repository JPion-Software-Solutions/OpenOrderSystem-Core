# Product Snapshots

**Model:** `Data/DataModels/V2/Catalog/ProductSnapshot.cs`  
**Written by:** `CatalogService` only (`StageSnapshotAsync`)

## What a snapshot is

An unchanging JSON record of a product as it existed at a point in time: the product, its variants,
media, metadata and option links (including each option and its effective price). Snapshots exist
for audit and for reconstructing history. They are **not** shaped by what a customer ordered; the
customer's actual selections are frozen onto `OrderLine` from the cart.

| Field | Meaning |
|---|---|
| `Id` | Snapshot identity. Ordering stamps this onto `OrderLine.ProductSnapshotId`. |
| `ProductId` | The product this captures. |
| `PreviousSnapshotId` | The snapshot it replaced, forming a per-product history chain. `null` for the first snapshot. |
| `SchemaVersion` | Shape of `SnapshotJson`. Currently `1`. |
| `SnapshotJson` | `CatalogProductDto` serialized with variants, media, metadata and option links. `CurrentSnapshot`, `IsArchived`, `Flags` and `FlagStates` inside it are always null. |
| `SnapshotHash` | SHA-256 (hex) of `SnapshotJson`. |
| `CreatedUtc` | When the snapshot was written. The latest one by this time is the "current" snapshot. |

## When snapshots are created

A snapshot is staged in the **same save** as the change that caused it, so the two commit or fail together.

| Operation | Snapshot |
|---|---|
| `AddProduct` | Always (the first one) |
| `UpdateProduct` | Only if the resulting graph hashes differently from the current snapshot |
| `UpdateVariantGroupPricing` | One per affected product (archived products included), again only if it changed |
| `ArchiveProduct` / `RestoreProduct` | Never. Archive state is lifecycle, not content |
| `UpdateProductFlags` | Never. Flags are operational state, not content |

**Deduplication:** if the new JSON hashes the same as the latest snapshot, nothing is written.
Repeated no-op saves don't grow the history.

## When snapshots are not created

These change data that appears in snapshots but **don't** create a new one, so the current
snapshot can go stale:

- `UpdateOption` and `DeleteOption` (option name, global price, flags; links removed by cascade)
- `UpdateMedia` (cover media details)
- Group changes

The next `UpdateProduct` on an affected product brings its snapshot up to date.

## How ordering uses them

```csharp
var product = (await catalog.FindProduct(line.ProductId, ProductLocatorFlags.IncludeSnapshot))
    .Results.SingleOrDefault();
var snapshotId = product?.CurrentSnapshot?.Id;   // -> OrderLine.ProductSnapshotId
```

If the product might have been archived after it went into the cart, the lookup also needs
`IncludeArchived`. Whether an order containing a newly archived product should be accepted is an
open design question.

Ordering never creates snapshots. What happens when a product has no snapshot at order time is an
open design question. In practice it shouldn't happen, because `AddProduct` always writes one.

## Known issues

- **Cover media and album can be stale in a snapshot.** When `UpdateProduct` changes `CoverMedia`
  or `MediaAlbum`, only the ID is set. The snapshot is taken before EF updates the loaded media to
  match, so the snapshot likely records the previous media.
