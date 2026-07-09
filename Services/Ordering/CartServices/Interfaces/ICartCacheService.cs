using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
namespace OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;

/// <summary>
/// Singleton service responsible for the cart in-memory cache, cold-start hydration
/// from the database, and garbage collection of abandoned and disposed carts.
/// </summary>
/// <remarks>
/// <para>
/// This service is the single entry point for all cart reads. Callers never go to the
/// database directly — <see cref="GetAsync"/> transparently handles cache hits, cold starts,
/// and missing carts, returning a <see cref="CartLookupResult"/> that covers all cases.
/// </para>
/// <para>
/// Cart mutations (writes) are owned by the cart service, which calls <see cref="UpdateCache"/>
/// after persisting changes to the database to keep the in-memory state current.
/// </para>
/// <para>
/// Garbage collection is driven by two independent conditions:
/// <list type="bullet">
/// <item><description>Carts with <see cref="Data.DataModels.V2.Ordering.Cart.IsDisposed"/> set are purged after a short TTL,
/// long enough to service idempotency checks.</description></item>
/// <item><description>Active carts whose <see cref="Data.DataModels.V2.Ordering.Cart.LastActive"/> exceeds the abandoned
/// cart threshold are purged regardless of disposal state.</description></item>
/// </list>
/// <see cref="SweepAsync"/> handles both conditions and is intended to be called by the cart GC Quartz job.
/// </para>
/// </remarks>
public interface ICartCacheService
{
    /// <summary>
    /// Retrieves a cart by ID from the in-memory cache, falling back to a cold-start
    /// database lookup if not cached.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart to retrieve.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/>,
    /// <see cref="CartLookupStatus.Disposed"/>, or <see cref="CartLookupStatus.NotFound"/>.
    /// </returns>
    Task<CartLookupResult> GetAsync(Guid cartId);

    /// <summary>
    /// Updates the in-memory cache entry for the given cart. Should be called by the
    /// cart service after every mutation is persisted to the database.
    /// </summary>
    /// <param name="cart">The cart record reflecting the latest persisted state.</param>
    void UpdateCache(Cart cart);

    /// <summary>
    /// Performs a garbage collection sweep, purging disposed carts past the short TTL
    /// and abandoned carts past the configured idle threshold from both the cache and
    /// the database.
    /// </summary>
    Task SweepAsync();
}
