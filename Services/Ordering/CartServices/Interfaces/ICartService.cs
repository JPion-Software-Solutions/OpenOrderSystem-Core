using OpenOrderSystem.Core.Services.Ordering.CartServices.Dto;

namespace OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;

/// <summary>
/// Scoped service responsible for cart business logic, including creation, mutation,
/// and lifecycle management.
/// </summary>
/// <remarks>
/// All cart reads are delegated to <see cref="ICartCacheService"/>. This service owns
/// writes only — it mutates the draft, persists changes to the database, and calls
/// <see cref="ICartCacheService.UpdateCache"/> to keep the cache current. It never
/// performs cache eviction or garbage collection.
/// <para>
/// Order projection is handled by the ordering service layer. The expected controller
/// flow on submission is: obtain draft via <see cref="GetAsync"/>, call the ordering
/// service to place the order, then call <see cref="SetOrderNumberAsync"/> followed by
/// <see cref="DisposeAsync"/> to close the cart.
/// </para>
/// </remarks>
public interface ICartService
{
    /// <summary>
    /// Creates a new cart, persists it to the database, and loads it into the cache.
    /// </summary>
    /// <returns>The newly created cart.</returns>
    Task<Data.DataModels.V2.Ordering.Cart> CreateAsync();

    /// <summary>
    /// Retrieves a cart by ID via the cache service.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> describing whether the cart was found, disposed,
    /// or does not exist.
    /// </returns>
    Task<CartLookupResult> GetAsync(Guid cartId);

    /// <summary>
    /// Stamps the order confirmation number onto the cart row. Called by the API layer
    /// after the ordering service has successfully placed the order.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <param name="orderNumber">The confirmation number assigned by the ordering service.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/> on success,
    /// or <see cref="CartLookupStatus.NotFound"/> if the cart does not exist.
    /// Returns the existing result unchanged if the cart is already disposed.
    /// </returns>
    Task<CartLookupResult> SetOrderNumberAsync(Guid cartId, int orderNumber);

    /// <summary>
    /// Marks the cart as disposed, making it eligible for short-TTL GC.
    /// Used for both post-submission cleanup and cancellation.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart to dispose.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Disposed"/>
    /// on success, or <see cref="CartLookupStatus.NotFound"/> if the cart does not exist.
    /// </returns>
    Task<CartLookupResult> DisposeAsync(Guid cartId);

    /// <summary>
    /// Adds a new line item to the cart. The service resolves the product and variant from
    /// the catalog to freeze display names and pricing onto the draft line at add time.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <param name="productId">The catalog product to add.</param>
    /// <param name="variantId">The selected variant of the product.</param>
    /// <param name="quantity">Number of units to add.</param>
    /// <param name="lineComments">Optional customer comments for this line.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/> on success,
    /// <see cref="CartLookupStatus.NotFound"/> if the cart does not exist, or
    /// <see cref="CartLookupStatus.Disposed"/> if the cart is no longer active.
    /// </returns>
    Task<CartLookupResult> AddItemAsync(Guid cartId, Guid productId, Guid variantId, int quantity, string? lineComments = null);

    /// <summary>
    /// Removes a line from the cart by its stable line identifier.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <param name="lineId">The <see cref="Dto.CartDraftLine.LineId"/> of the line to remove.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/> on success,
    /// <see cref="CartLookupStatus.NotFound"/> if the cart or line does not exist, or
    /// <see cref="CartLookupStatus.Disposed"/> if the cart is no longer active.
    /// </returns>
    Task<CartLookupResult> RemoveItemAsync(Guid cartId, Guid lineId);

    /// <summary>
    /// Updates the quantity and/or comments on an existing cart line.
    /// <see langword="null"/> arguments leave the corresponding field unchanged.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <param name="lineId">The <see cref="Dto.CartDraftLine.LineId"/> of the line to update.</param>
    /// <param name="quantity">New quantity, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="lineComments">New line comments, or <see langword="null"/> to leave unchanged.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/> on success,
    /// <see cref="CartLookupStatus.NotFound"/> if the cart or line does not exist, or
    /// <see cref="CartLookupStatus.Disposed"/> if the cart is no longer active.
    /// </returns>
    Task<CartLookupResult> UpdateItemAsync(Guid cartId, Guid lineId, int? quantity, string? lineComments);

    /// <summary>
    /// Replaces the option selections on an existing cart line. The service resolves
    /// option display names and pricing from the catalog. Passing an empty collection
    /// clears all options from the line.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <param name="lineId">The <see cref="Dto.CartDraftLine.LineId"/> of the line to update.</param>
    /// <param name="options">The new option selections to apply.</param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/> on success,
    /// <see cref="CartLookupStatus.NotFound"/> if the cart or line does not exist, or
    /// <see cref="CartLookupStatus.Disposed"/> if the cart is no longer active.
    /// </returns>
    Task<CartLookupResult> SetLineOptionsAsync(Guid cartId, Guid lineId, IEnumerable<CartLineOptionRequest> options);

    /// <summary>
    /// Updates order-level metadata on the draft. By default, <see langword="null"/> arguments
    /// are ignored. Set bits in <paramref name="resetFields"/> to treat specific
    /// <see langword="null"/> arguments as explicit clears instead.
    /// </summary>
    /// <param name="cartId">The unique identifier of the cart.</param>
    /// <param name="customer">Replacement customer info, or <see langword="null"/> to leave unchanged / clear.</param>
    /// <param name="orderComments">Replacement order comments, or <see langword="null"/> to leave unchanged / clear.</param>
    /// <param name="requestedTimeSlot">Replacement requested pickup time, or <see langword="null"/> to leave unchanged / clear.</param>
    /// <param name="resetFields">
    /// Flags indicating which <see langword="null"/> arguments should clear their corresponding field.
    /// Fields whose flag is not set are left unchanged when their argument is <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A <see cref="CartLookupResult"/> with status <see cref="CartLookupStatus.Found"/> on success,
    /// <see cref="CartLookupResult.NotFound"/> if the cart does not exist, or
    /// <see cref="CartLookupStatus.Disposed"/> if the cart is no longer active.
    /// </returns>
    Task<CartLookupResult> UpdateCartMetadataAsync(Guid cartId, CartCustomerInfo? customer = null, string? orderComments = null, DateTimeOffset? requestedTimeSlot = null, CartMetadataResetFlags resetFields = CartMetadataResetFlags.None);
}
