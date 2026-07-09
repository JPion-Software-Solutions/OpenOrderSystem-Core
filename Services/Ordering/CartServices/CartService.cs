using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Catalog;
using OpenOrderSystem.Core.Services.Catalog.Dto;
using OpenOrderSystem.Core.Services.Catalog.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Dto;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.CartServices;

/// <summary>
/// Default scoped implementation of <see cref="ICartService"/>.
/// </summary>
public class CartService : ICartService
{
    private readonly ICartCacheService _cache;
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly IReadOnlyCatalog _catalog;
    private readonly ILogger<CartService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CartService"/>.
    /// </summary>
    public CartService(
        ICartCacheService cache,
        IDbContextFactory<OosDbContext> contextFactory,
        IReadOnlyCatalog catalog,
        ILogger<CartService> logger)
    {
        _cache = cache;
        _contextFactory = contextFactory;
        _catalog = catalog;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Cart> CreateAsync()
    {
        var cart = new Cart
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            LastActive = DateTimeOffset.UtcNow
        };

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        _cache.UpdateCache(cart);

        _logger.LogDebug("Created cart '{CartId}'.", cart.Id);

        return cart;
    }

    /// <inheritdoc/>
    public Task<CartLookupResult> GetAsync(Guid cartId)
        => _cache.GetAsync(cartId);

    /// <inheritdoc/>
    public async Task<CartLookupResult> SetOrderNumberAsync(Guid cartId, int orderNumber)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status == CartLookupStatus.NotFound)
            return CartLookupResult.NotFound();

        if (lookup.Status == CartLookupStatus.Disposed)
            return lookup;

        var cart = lookup.Cart!;
        cart.OrderConfirmation = orderNumber;

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Carts.Update(cart);
        await db.SaveChangesAsync();

        _cache.UpdateCache(cart);

        _logger.LogDebug("Cart '{CartId}' — order number set to '{OrderNumber}'.", cartId, orderNumber);

        return CartLookupResult.Found(cart);
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> DisposeAsync(Guid cartId)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status == CartLookupStatus.NotFound)
            return CartLookupResult.NotFound();

        if (lookup.Status == CartLookupStatus.Disposed)
            return lookup;

        var cart = lookup.Cart!;
        cart.IsDisposed = true;
        cart.LastActive = DateTimeOffset.UtcNow;

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Carts.Update(cart);
        await db.SaveChangesAsync();

        _cache.UpdateCache(cart);

        _logger.LogDebug("Cart '{CartId}' disposed.", cartId);

        return CartLookupResult.Disposed(cart);
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> AddItemAsync(Guid cartId, Guid productId, Guid variantId, int quantity, string? lineComments = null)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status != CartLookupStatus.Found)
            return lookup;

        var productResult = await _catalog.FindProduct(productId, ProductLocatorFlags.IncludeVariants);
        var product = productResult.Results.SingleOrDefault();
        var variant = product?.Variants?.FirstOrDefault(v => v.Id == variantId);

        if (!productResult.IsSuccess || product is null || variant is null)
        {
            _logger.LogWarning("Cart '{CartId}' — AddItemAsync: variant '{VariantId}' on product '{ProductId}' not found in catalog.",
                cartId, variantId, productId);
            return CartLookupResult.NotFound();
        }

        var draft = DeserializeDraft(lookup.Cart!);

        draft.Lines.Add(new CartDraftLine
        {
            ProductId = productId,
            VariantId = variantId,
            ProductName = product.Name ?? string.Empty,
            VariantName = variant.Name ?? string.Empty,
            BasePrice = variant.Price ?? 0m,
            Quantity = quantity,
            LineComments = lineComments
        });

        _logger.LogDebug("Cart '{CartId}' — added line for variant '{VariantId}'.", cartId, variantId);

        return await SaveDraftAsync(lookup.Cart!, draft);
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> RemoveItemAsync(Guid cartId, Guid lineId)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status != CartLookupStatus.Found)
            return lookup;

        var draft = DeserializeDraft(lookup.Cart!);
        var removed = draft.Lines.RemoveAll(l => l.LineId == lineId);

        if (removed == 0)
        {
            _logger.LogWarning("Cart '{CartId}' — RemoveItemAsync found no line with id '{LineId}'.", cartId, lineId);
            return CartLookupResult.NotFound();
        }

        _logger.LogDebug("Cart '{CartId}' — removed line '{LineId}'.", cartId, lineId);

        return await SaveDraftAsync(lookup.Cart!, draft);
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> UpdateItemAsync(Guid cartId, Guid lineId, int? quantity, string? lineComments)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status != CartLookupStatus.Found)
            return lookup;

        var draft = DeserializeDraft(lookup.Cart!);
        var line = draft.Lines.FirstOrDefault(l => l.LineId == lineId);

        if (line is null)
        {
            _logger.LogWarning("Cart '{CartId}' — UpdateItemAsync found no line with id '{LineId}'.", cartId, lineId);
            return CartLookupResult.NotFound();
        }

        if (quantity.HasValue)
            line.Quantity = quantity.Value;

        if (lineComments is not null)
            line.LineComments = lineComments;

        _logger.LogDebug("Cart '{CartId}' — updated line '{LineId}'.", cartId, lineId);

        return await SaveDraftAsync(lookup.Cart!, draft);
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> SetLineOptionsAsync(Guid cartId, Guid lineId, IEnumerable<CartLineOptionRequest> options)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status != CartLookupStatus.Found)
            return lookup;

        var draft = DeserializeDraft(lookup.Cart!);
        var line = draft.Lines.FirstOrDefault(l => l.LineId == lineId);

        if (line is null)
        {
            _logger.LogWarning("Cart '{CartId}' — SetLineOptionsAsync found no line with id '{LineId}'.", cartId, lineId);
            return CartLookupResult.NotFound();
        }

        var requests = options.ToList();

        // Resolve all options from the catalog service before mutating the cart.
        var optionResultTasks = requests.Select(r => _catalog.FindOption(r.OptionId));
        var optionResults = await Task.WhenAll(optionResultTasks);

        var catalogOptions = new Dictionary<Guid, CatalogOptionDto>();
        for (var i = 0; i < requests.Count; i++)
        {
            var result = optionResults[i];
            var option = result.Results.SingleOrDefault();
            if (!result.IsSuccess || option?.Id is null)
            {
                _logger.LogWarning("Cart '{CartId}' — SetLineOptionsAsync: option '{OptionId}' not found in catalog.",
                    cartId, requests[i].OptionId);
                return CartLookupResult.NotFound();
            }
            catalogOptions[option.Id.Value] = option;
        }

        line.SelectedOptions = requests.Select(r =>
        {
            var opt = catalogOptions[r.OptionId];
            var delta = opt.PriceDelta ?? 0m;
            // EffectiveDelta is 0 for Included options (already baked into BasePrice).
            // For Added/Removed, the effective delta may differ from the global option delta
            // when the product carries a per-product price override — resolution belongs in the
            // catalog service. Add IReadOnlyCatalog.ResolveEffectiveDelta(productId, optionId)
            // (or equivalent) when that use case needs to be frozen here.
            var effectiveDelta = r.SelectionState == OptionSelectionState.Included ? 0m : delta;
            return new CartDraftOption
            {
                OptionId = r.OptionId,
                OptionName = opt.Name ?? string.Empty,
                Delta = delta,
                EffectiveDelta = effectiveDelta,
                Quantity = r.Quantity,
                SelectionState = r.SelectionState
            };
        }).ToList();

        _logger.LogDebug("Cart '{CartId}' — set {Count} option(s) on line '{LineId}'.", cartId, requests.Count, lineId);

        return await SaveDraftAsync(lookup.Cart!, draft);
    }

    /// <inheritdoc/>
    public async Task<CartLookupResult> UpdateCartMetadataAsync(Guid cartId, CartCustomerInfo? customer = null, string? orderComments = null, DateTimeOffset? requestedTimeSlot = null, CartMetadataResetFlags resetFields = CartMetadataResetFlags.None)
    {
        var lookup = await _cache.GetAsync(cartId);

        if (lookup.Status != CartLookupStatus.Found)
            return lookup;

        var draft = DeserializeDraft(lookup.Cart!);

        if (customer is not null || resetFields.HasFlag(CartMetadataResetFlags.Customer))
            draft.Customer = customer;

        if (orderComments is not null || resetFields.HasFlag(CartMetadataResetFlags.OrderComments))
            draft.OrderComments = orderComments;

        if (requestedTimeSlot is not null || resetFields.HasFlag(CartMetadataResetFlags.RequestedTimeSlot))
            draft.RequestedTimeSlot = requestedTimeSlot;

        _logger.LogDebug("Cart '{CartId}' — metadata updated.", cartId);

        return await SaveDraftAsync(lookup.Cart!, draft);
    }

    private static CartDraft DeserializeDraft(Cart cart)
    {
        if (string.IsNullOrWhiteSpace(cart.DraftOrder))
            return new CartDraft();

        try { return JsonSerializer.Deserialize<CartDraft>(cart.DraftOrder) ?? new CartDraft(); }
        catch { return new CartDraft(); }
    }

    private async Task<CartLookupResult> SaveDraftAsync(Cart cart, CartDraft draft)
    {
        cart.DraftOrder = JsonSerializer.Serialize(draft);
        cart.LastActive = DateTimeOffset.UtcNow;

        await using var db = await _contextFactory.CreateDbContextAsync();
        db.Carts.Update(cart);
        await db.SaveChangesAsync();

        _cache.UpdateCache(cart);

        return CartLookupResult.Found(cart);
    }
}
