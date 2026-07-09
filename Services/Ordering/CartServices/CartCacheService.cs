using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.CartServices;

/// <summary>
/// Default singleton implementation of <see cref="ICartCacheService"/>.
/// </summary>
public class CartCacheService : ICartCacheService
{
    private readonly ConcurrentDictionary<Guid, Data.DataModels.V2.Ordering.Cart> _cache = new();
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly ILogger<CartCacheService> _logger;
    private readonly IConfigurationStore _configStore;

    /// <summary>
    /// Initializes a new instance of <see cref="CartCacheService"/>.
    /// </summary>
    public CartCacheService(
        IDbContextFactory<OosDbContext> contextFactory,
        ILogger<CartCacheService> logger, IConfigurationStore configStore)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _configStore = configStore;
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> GetAsync(Guid cartId)
    {
        if (_cache.TryGetValue(cartId, out var cached))
        {
            return cached.IsDisposed
                ? CartLookupResult.Disposed(cached)
                : CartLookupResult.Found(cached);
        }

        // Cold start — check the database
        await using var db = await _contextFactory.CreateDbContextAsync();
        var cart = await db.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cartId);

        if (cart is null)
        {
            _logger.LogDebug("Cart '{CartId}' not found in cache or database.", cartId);
            return CartLookupResult.NotFound();
        }

        _cache[cartId] = cart;

        _logger.LogDebug("Cart '{CartId}' loaded from database (cold start).", cartId);

        return cart.IsDisposed
            ? CartLookupResult.Disposed(cart)
            : CartLookupResult.Found(cart);
    }

    /// <inheritdoc/>
    public void UpdateCache(Cart cart)
        => _cache[cart.Id] = cart;

    /// <inheritdoc/>
    public async Task SweepAsync()
    {
        var disposedTtlMinutes = await _configStore.GetConfigurationAsync<int>("cart.disposedTtlMinutes", 5);
        var abandonedTtlMinutes = await _configStore.GetConfigurationAsync<int>("cart.disposedTtlMinutes", 120);
        
        var disposedTtl = TimeSpan.FromMinutes(disposedTtlMinutes);
        var abandonedTtl = TimeSpan.FromMinutes(abandonedTtlMinutes);
        var now = DateTimeOffset.UtcNow;

        await using var db = await _contextFactory.CreateDbContextAsync();

        var toDelete = await db.Carts
            .Where(c =>
                (c.IsDisposed && c.LastActive < now - disposedTtl) ||
                (!c.IsDisposed && c.LastActive < now - abandonedTtl))
            .ToListAsync();

        if (toDelete.Count == 0)
            return;

        foreach (var cart in toDelete)
            _cache.TryRemove(cart.Id, out _);

        db.Carts.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        _logger.LogInformation("Cart GC swept {Count} cart(s).", toDelete.Count);
    }
}
