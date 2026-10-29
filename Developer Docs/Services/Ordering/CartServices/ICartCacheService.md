# ICartCacheService

`Services/Ordering/CartServices/Interfaces/ICartCacheService.cs`, implemented by `CartCacheService` (singleton).

The **only** entry point for reading carts. Nothing else should query `Carts` directly.

## `GetAsync(Guid cartId)` → `CartLookupResult`

1. **Cached:** returns `Disposed` if `IsDisposed`, otherwise `Found`.
2. **Not cached:** loads the row from the database without tracking, caches it, then returns the same as above.
3. **Not in the database either:** returns `NotFound`.

Cached carts are kept until `SweepAsync` purges them. There is no time-based eviction of cache
entries apart from that.

## `UpdateCache(Cart cart)`

Adds or replaces the cache entry. `CartService` calls this after every successful save. Don't call
it with a cart that hasn't been saved, or the cache and database will disagree.

## `SweepAsync()`

Deletes carts from the database and removes them from the cache when either:
- `IsDisposed` and `LastActive` is older than the disposed TTL (default 5 min), or
- not disposed and `LastActive` is older than the abandoned TTL (default 120 min).

TTL settings are covered in [README → Configuration](README.md#configuration). Intended to be run by
a cart garbage-collection Quartz job, **which doesn't exist yet**.

## Known issues

See [README → Known issues](README.md#known-issues): the sweep isn't scheduled yet, and cache entries are
shared mutable instances.
