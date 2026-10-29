# Cart Services

The working, pre-submission side of ordering: create a cart, add and edit lines, capture customer
details, then hand the draft to order placement.

**Source:** `Services/Ordering/CartServices/`

## Topics

| File | Covers |
|---|---|
| [ICartService.md](ICartService.md) | All cart operations (create, items, options, metadata, close-out) |
| [ICartCacheService.md](ICartCacheService.md) | Cache reads, cold start, garbage collection |
| [Cart-Types.md](Cart-Types.md) | `CartDraft` and its line/option/customer types, `CartLookupResult`, `CartMetadataResetFlags`, line totals |

## Architecture

| Type | Lifetime | Responsibility |
|---|---|---|
| `ICartCacheService` / `CartCacheService` | Singleton | **All reads.** An in-memory `ConcurrentDictionary` keyed by cart ID, filled from the database when a cart isn't cached. Also does garbage collection. |
| `ICartService` / `CartService` | Scoped | **All writes.** Updates the draft, saves it, then calls `UpdateCache`. |
| `Cart` (model) | — | The row: `DraftOrder` holds the JSON-serialized `CartDraft`. Never read or write `DraftOrder` directly. |

Every write is saved to the database right away, so carts survive restarts. The cache only avoids
repeated database reads.

## Cart lifecycle

```
CreateAsync ──► Active ──(add/edit lines, options, metadata)──► Active
                  │
                  ├─ order placed ─► SetOrderNumberAsync ─► DisposeAsync ─► Disposed ─(short TTL)─► purged
                  ├─ cancelled ───────────────────────────► DisposeAsync ─► Disposed ─(short TTL)─► purged
                  └─ idle past abandoned TTL ─────────────────────────────────────────────────────► purged
```

A disposed cart stays readable for a short time. If a client resubmits, `GetAsync` returns
`Disposed` and the caller can return `Cart.OrderConfirmation` instead of placing a second order.

## Submission

Submission is handled entirely by [`IOrderPlacementService.PlaceOrderAsync`](../Placement/README.md). After the order
is saved it stores the confirmation number on the cart (`SetOrderNumberAsync`) and disposes the cart, and it
answers a duplicate submit with `AlreadyPlaced`. Callers don't need to close the cart themselves.

## Configuration

Read through `IConfigurationStore` during `SweepAsync`. The store is scoped, so the singleton cache resolves it
from a short-lived scope created with `IServiceScopeFactory`:

| Key | Default | Meaning |
|---|---|---|
| `cart.disposedTtlMinutes` | 5 | Minutes a disposed cart is kept |
| `cart.abandonedTtlMinutes` | 120 | Minutes of inactivity before an active cart is purged |

## Registration status

**Not registered yet, deliberately.** V2 services will be registered together through a single extension
method once the end-to-end ordering flow is complete. (`RunOOS.cs` currently registers the *legacy*
`Services/Legacy/CartService.cs`.) The registrations the cart needs:

```csharp
services.AddSingleton<ICartCacheService, CartCacheService>();
services.AddScoped<ICartService, CartService>();   // needs IReadOnlyCatalog registered
```

## Known issues

- **Nothing schedules `SweepAsync`.** There is no Quartz job yet, so carts are never purged.
- **The cache shares mutable `Cart` instances between requests.** Two simultaneous writes to the same
  cart can race, and the last write wins. This is acceptable for one customer per cart, but worth knowing.
- **A corrupt `DraftOrder` is replaced silently.** If the JSON doesn't deserialize, the service starts
  from an empty draft and overwrites it on the next save.
- **Order number type mismatch:** `Cart.OrderConfirmation` and `SetOrderNumberAsync` use `int`,
  but `OrderHeader.OrderNumber` is `long`.
