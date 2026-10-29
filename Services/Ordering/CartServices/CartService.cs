using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;
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

        if (quantity < 1)
            return CartLookupResult.Rejected(lookup.Cart!, "Quantity must be at least 1.");

        var productResult = await _catalog.FindProduct(productId, ProductLocatorFlags.IncludeVariants | ProductLocatorFlags.IncludeOptions);
        var product = productResult.Results.SingleOrDefault();
        var variant = product?.Variants?.FirstOrDefault(v => v.Id == variantId);

        if (!productResult.IsSuccess || product is null || variant is null)
        {
            _logger.LogWarning("Cart '{CartId}' — AddItemAsync: variant '{VariantId}' on product '{ProductId}' not found in catalog.",
                cartId, variantId, productId);
            return CartLookupResult.NotFound();
        }

        if (UnavailableReason(product) is { } reason)
            return CartLookupResult.Rejected(lookup.Cart!, reason);

        var draft = DeserializeDraft(lookup.Cart!);

        // Every option the product includes by default starts on the line, so the line always
        // describes the full build and customers can only remove or adjust defaults, not drop them silently.
        draft.Lines.Add(new CartDraftLine
        {
            ProductId = productId,
            VariantId = variantId,
            ProductName = product.Name ?? string.Empty,
            VariantName = variant.Name ?? string.Empty,
            BasePrice = variant.Price ?? 0m,
            Quantity = quantity,
            LineComments = lineComments,
            SelectedOptions = (product.Options ?? [])
                .Where(l => l.DefaultState == ProductOptionDefaultState.Included)
                .Select(l => ToDraftOption(l, OptionSelectionState.Included, 1.0m))
                .ToList()
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

        if (quantity < 1)
            return CartLookupResult.Rejected(lookup.Cart!, "Quantity must be at least 1.");

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

        if (requests.Select(r => r.OptionId).Distinct().Count() != requests.Count)
            return CartLookupResult.Rejected(lookup.Cart!, "Each option may only appear once.");
        if (requests.Any(r => r.Quantity < 0m))
            return CartLookupResult.Rejected(lookup.Cart!, "Option quantity cannot be negative.");

        // Selection state and pricing come from the product's option links, never from the request.
        var productResult = await _catalog.FindProduct(line.ProductId, ProductLocatorFlags.IncludeOptions);
        var product = productResult.Results.SingleOrDefault();
        if (!productResult.IsSuccess || product is null)
            return CartLookupResult.Rejected(lookup.Cart!, $"{line.ProductName} is no longer available.");

        var links = (product.Options ?? []).Where(l => l.OptionId.HasValue).ToDictionary(l => l.OptionId!.Value);
        var requested = requests.ToDictionary(r => r.OptionId);

        var unlinked = requests.FirstOrDefault(r => !links.ContainsKey(r.OptionId));
        if (unlinked is not null)
        {
            _logger.LogWarning("Cart '{CartId}' — SetLineOptionsAsync: option '{OptionId}' is not offered on product '{ProductId}'.",
                cartId, unlinked.OptionId, line.ProductId);
            return CartLookupResult.Rejected(lookup.Cart!, $"That option is not available on {line.ProductName}.");
        }

        var selected = new List<CartDraftOption>();
        foreach (var link in links.Values)
        {
            requested.TryGetValue(link.OptionId!.Value, out var request);
            var removing = request is not null && (request.SelectionState == OptionSelectionState.Removed || request.Quantity == 0m);

            if (link.DefaultState == ProductOptionDefaultState.Included)
            {
                // Defaults are always on the line: kept (optionally light/extra) or explicitly removed.
                selected.Add(removing
                    ? ToDraftOption(link, OptionSelectionState.Removed, 0m)
                    : ToDraftOption(link, OptionSelectionState.Included, request?.Quantity ?? 1.0m));
                continue;
            }

            if (request is null)
                continue;
            if (removing)
                return CartLookupResult.Rejected(lookup.Cart!, $"{link.Option?.Name} is not included on {line.ProductName}, so it cannot be removed.");
            if (UnavailableReason(link.Option) is { } reason)
                return CartLookupResult.Rejected(lookup.Cart!, reason);

            selected.Add(ToDraftOption(link, OptionSelectionState.Added, request.Quantity));
        }

        line.SelectedOptions = selected;

        _logger.LogDebug("Cart '{CartId}' — set {Count} option(s) on line '{LineId}'.", cartId, selected.Count, lineId);

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

    /// <summary>
    /// Freezes a catalog option link onto a draft line. <see cref="CartDraftOption.Delta"/> is the option's price
    /// on this product; only <see cref="OptionSelectionState.Added"/> options contribute it to the line total.
    /// </summary>
    private static CartDraftOption ToDraftOption(CatalogProductOptionDto link, OptionSelectionState state, decimal quantity)
    {
        var delta = link.PriceDeltaOverride ?? link.Option?.PriceDelta ?? 0m;
        return new CartDraftOption
        {
            OptionId = link.OptionId!.Value,
            OptionName = link.Option?.Name ?? string.Empty,
            Delta = delta,
            EffectiveDelta = state == OptionSelectionState.Added ? delta : 0m,
            Quantity = quantity,
            SelectionState = state
        };
    }

    private static string? UnavailableReason(CatalogProductDto product) => product.Flags switch
    {
        { } f when f.HasFlag(ProductFlags.OutOfStock) => $"{product.Name} is out of stock.",
        { } f when f.HasFlag(ProductFlags.Hidden)     => $"{product.Name} is not available.",
        _ => null
    };

    private static string? UnavailableReason(CatalogOptionDto? option) => option?.Flags switch
    {
        null => "That option is not available.",
        { } f when f.HasFlag(OptionFlags.OutOfStock) => $"{option.Name} is out of stock.",
        { } f when (f & (OptionFlags.Disabled | OptionFlags.Hidden)) != 0 => $"{option.Name} is not available.",
        _ => null
    };

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
