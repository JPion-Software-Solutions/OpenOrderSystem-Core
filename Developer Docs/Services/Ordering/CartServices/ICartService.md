# ICartService

`Services/Ordering/CartServices/Interfaces/ICartService.cs`, implemented by `CartService` (scoped).

Owns every cart write. Reads are delegated to [`ICartCacheService`](ICartCacheService.md).

## Common behavior

- **Every mutation follows the same pattern:** look up the cart → if it's not `Found`, return that
  lookup unchanged → deserialize the draft → change it → serialize it, set `LastActive`, save to
  the database → `UpdateCache` → return `Found` with the updated cart.
- **Mutations require an active cart.** A disposed cart returns `Disposed` and is left untouched.
- **`NotFound` vs. `Rejected`:** `NotFound` means the cart, line, product or variant you referenced
  doesn't exist. `Rejected` means it exists but the change isn't allowed (out of stock, option not offered,
  bad quantity, …). In that case the cart is unchanged and `Message` holds a customer-presentable reason.
- **Prices and names are frozen** when an item is added or options are set. Catalog changes during a
  session don't affect existing lines.

## Lifecycle

### `CreateAsync()` → `Cart`
Creates an empty cart, saves it, and caches it. Returns the `Cart` entity; its `Id` is the cart
reference you give the client.

### `GetAsync(Guid cartId)` → `CartLookupResult`
Passes straight through to `ICartCacheService.GetAsync`. Returns `Found`, `Disposed` or `NotFound`.
To read the draft, deserialize `Cart.DraftOrder` into a `CartDraft`.

### `SetOrderNumberAsync(Guid cartId, int orderNumber)` → `CartLookupResult`
Stores the confirmation number on the cart after the order has been placed. Called by order placement; API code doesn't need to. Works only on an active
cart; a disposed cart is returned unchanged. Call this **before** `DisposeAsync`.

### `DisposeAsync(Guid cartId)` → `CartLookupResult`
Marks the cart disposed (used after submission and on cancellation), which starts the short disposed
TTL. Returns `Disposed`. Calling it on a cart that is already disposed is harmless and returns `Disposed` again.

## Line items

### `AddItemAsync(cartId, productId, variantId, quantity, lineComments = null)`
Looks up the product with its variants and option links, then appends a new `CartDraftLine` with
`ProductName`, `VariantName` and `BasePrice` (the variant's `Price`) frozen. Every option the product
includes by default goes onto the line as `Included` (quantity 1, no charge), so a line always describes
the full build. Each call adds a **new line**, even if the same variant is already in the cart; lines are never merged.

| Result | When |
|---|---|
| `NotFound` | Product or variant doesn't exist, or the product is archived |
| `Rejected` | `quantity` < 1, or the product is `OutOfStock` or `Hidden` |

### `RemoveItemAsync(cartId, lineId)`
Removes the line with that `LineId`. `NotFound` if no line matches.

### `UpdateItemAsync(cartId, lineId, quantity, lineComments)`
Partial update: a `null` argument leaves that field unchanged. Comments can't be cleared to null
(pass an empty string instead). `Rejected` if `quantity` < 1.

### `SetLineOptionsAsync(cartId, lineId, IEnumerable<CartLineOptionRequest> options)`
Applies the customer's option choices to a line. **The request only says what the customer wants. Whether
an option is included and what it costs always come from the product's option links in the catalog**,
never from the client.

The line is rebuilt from the product's current option links:

| Option on the product | In the request | Result on the line |
|---|---|---|
| Included by default | Absent | `Included`, quantity 1, no charge |
| Included by default | Present | `Included` at the requested quantity (light/extra), no charge |
| Included by default | `Removed` or quantity 0 | `Removed`, no charge |
| Optional | Absent | Not on the line |
| Optional | Present | `Added`, charged the product's price for it (`PriceDeltaOverride`, else the option's `PriceDelta`) |
| Optional | `Removed` or quantity 0 | `Rejected` (it isn't included, so it can't be removed) |
| Not linked to the product | Present | `Rejected` |

An empty list resets the line to the product defaults. The request's `SelectionState` only matters
when it is `Removed`; `Included` and `Added` both just mean "I want this".

Also `Rejected` when: an added option is `OutOfStock`, `Disabled` or `Hidden`; an option appears twice; a
quantity is negative; or the product has since been archived. Nothing is changed when a call is rejected.

Option `Quantity` is stored but doesn't affect price. "Extra" currently costs the same as standard.

## Order-level metadata

### `UpdateCartMetadataAsync(cartId, customer = null, orderComments = null, requestedTimeSlot = null, resetFields = None)`
Sets the draft's `Customer`, `OrderComments` and `RequestedTimeSlot`. A `null` argument means
**unchanged**, unless the matching bit in [`CartMetadataResetFlags`](Cart-Types.md#cartmetadataresetflags)
is set, in which case `null` **clears** that field.

```csharp
// Switch the cart back to ASAP and leave everything else alone
await carts.UpdateCartMetadataAsync(cartId, resetFields: CartMetadataResetFlags.RequestedTimeSlot);
```

`RequestedTimeSlot == null` means ASAP. The requested time is stored as given; it isn't checked
against slot availability (that will be timekeeping's job).
