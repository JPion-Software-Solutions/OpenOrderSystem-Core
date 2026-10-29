# Product Variant & Option Sync

How `AddProduct` and `UpdateProduct` handle `CatalogProductDto.Variants` and `CatalogProductDto.Options`.
This is the only way to add, change or remove variants, and the only way to link options to products.

## Why it works this way

- **Variants belong to exactly one product**, so they are edited as part of it. An edit form loads the
  product, changes it, and sends the whole thing back.
- **Options exist on their own** (see [ICatalogManager.md](ICatalogManager.md#options)). The product
  only owns its *links* to options: the default state and an optional per-product price override.
- One product edit is one transaction and creates at most one [snapshot](Product-Snapshots.md).

## Rule 1: `null` vs. a list

| Value | Meaning |
|---|---|
| `null` | Leave this collection exactly as it is. |
| A list, even an empty one | This is the **complete set**. Anything not in the list is removed. |

An empty `Variants` list deletes every variant, which is allowed: a product with no variants simply
can't be ordered. An empty `Options` list unlinks every option.

## Rule 2: Matching

**Variants** are matched by `Id`:

| Entry | Result |
|---|---|
| `Id` set and belongs to this product | Update that variant |
| `Id` is null | Create a new variant |
| `Id` set but not one of this product's variants | `ValidationError`, nothing is saved |
| Existing variant not in the list | Deleted |

**Option links** are matched by `OptionId`:

| Entry | Result |
|---|---|
| Option already linked | Update the link |
| Option not linked yet | Create the link |
| Existing link not in the list | Remove the link (the option itself stays) |

## Rule 3: Each entry is full state

Unlike product-level fields, each entry in these lists replaces the stored record completely.

**Variant entry:**

| Field | If `null` |
|---|---|
| `Name`, `Price` | `ValidationError` (required) |
| `StrikePrice`, `Cost`, `Sku`, `Barcode`, `GroupId` | **Cleared** |
| `Metadata` | **All metadata cleared** (a supplied dictionary replaces the old one entirely, rather than merging) |
| `ProductId` | Fine. If set, it must equal the product being saved |

**Option link entry:**

| Field | If `null` |
|---|---|
| `OptionId` | `ValidationError` (required) |
| `DefaultState` | Becomes `Optional` |
| `PriceDeltaOverride` | **Cleared** (falls back to the option's global `PriceDelta`) |
| `ProductId` | Fine. If set, it must equal the product being saved |
| `Option`, `EffectivePriceDelta` | Ignored; these are read-only |

> ⚠️ **Always send complete variants.** Sending `{ Id, Price }` to change a price wipes the SKU,
> barcode, cost, strike price, group and metadata. Load the product with `IncludeVariants`
> (and `IncludeMetadata`), change it, and send it back.

## Validation

Everything below is checked before anything is changed. If any check fails, nothing is written.

| Check | Status |
|---|---|
| Variant `Name` blank, or `Price` null or negative | `ValidationError` |
| Variant `ProductId` set to a different product | `ValidationError` |
| Variant `Id` not belonging to this product (or any `Id` on `AddProduct`) | `ValidationError` |
| Same variant `Id` twice in the request | `ValidationError` |
| A variant `GroupId` that does not exist | `ValidationError` |
| Same `Sku` twice in the request | `Conflict` |
| `Sku` already used by a variant of another product | `Conflict` |
| Option link with null `OptionId`, a mismatched `ProductId`, or a duplicate `OptionId` | `ValidationError` |
| An `OptionId` that doesn't exist | `ValidationError` |

`StrikePrice` is not required to be greater than `Price`.

SKUs are also enforced by the unique database index `UIX_Variant_Sku` (NULLs are excluded).
**Known limitation:** swapping SKUs between two existing variants in one update will probably be
rejected by that index, and the call returns `StorageError`. Do the swap in two updates.

## Example

```csharp
var current = (await catalog.FindProduct(id,
    ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeOptions | ProductLocatorFlags.IncludeMetadata))
    .Results.Single();

var variants = current.Variants!.ToList();
variants[0] = variants[0] with { Price = 12.50m };                      // edit, keeping every other field
variants.Add(new CatalogVariantDto(null, null, null, "Large", 15m,       // new variant (Id = null)
    null, null, "PIZ-LG", null, null));

var options = current.Options!.ToList();
options.Add(new CatalogProductOptionDto(null, extraCheeseId,             // new link
    ProductOptionDefaultState.Optional, PriceDeltaOverride: 1.00m));

var result = await catalog.UpdateProduct(current with { Variants = variants, Options = options });
```
