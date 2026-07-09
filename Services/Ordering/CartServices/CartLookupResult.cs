using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

namespace OpenOrderSystem.Core.Services.Ordering.CartServices;

/// <summary>
/// Describes the outcome of a cart lookup performed by <see cref="Interfaces.ICartCacheService"/>.
/// </summary>
/// <remarks>
/// Callers should switch on <see cref="Status"/> before accessing <see cref="Cart"/>:
/// <list type="bullet">
/// <item><description><see cref="CartLookupStatus.Found"/> — <see cref="Cart"/> is populated and ready for use.</description></item>
/// <item><description><see cref="CartLookupStatus.NotFound"/> — no cart exists for the requested ID; <see cref="Cart"/> is <see langword="null"/>.</description></item>
/// <item><description><see cref="CartLookupStatus.Disposed"/> — the cart was submitted or cancelled; inspect <see cref="Cart.OrderConfirmation"/>
/// to determine whether an order was created and return the confirmation number for idempotency.</description></item>
/// </list>
/// </remarks>
public class CartLookupResult
{
    /// <summary>
    /// The outcome of the lookup.
    /// </summary>
    public CartLookupStatus Status { get; init; }

    /// <summary>
    /// The located cart, or <see langword="null"/> when <see cref="Status"/> is <see cref="CartLookupStatus.NotFound"/>.
    /// </summary>
    public Data.DataModels.V2.Ordering.Cart? Cart { get; init; }

    /// <summary>
    /// Creates a result indicating the cart was found and is active.
    /// </summary>
    public static CartLookupResult Found(Data.DataModels.V2.Ordering.Cart cart) => new()
    {
        Status = CartLookupStatus.Found,
        Cart = cart
    };

    /// <summary>
    /// Creates a result indicating no cart exists for the requested ID.
    /// </summary>
    public static CartLookupResult NotFound() => new()
    {
        Status = CartLookupStatus.NotFound,
        Cart = null
    };

    /// <summary>
    /// Creates a result indicating the cart has been disposed.
    /// </summary>
    public static CartLookupResult Disposed(Data.DataModels.V2.Ordering.Cart cart) => new()
    {
        Status = CartLookupStatus.Disposed,
        Cart = cart
    };
}

/// <summary>
/// Controls which <see langword="null"/> arguments to <see cref="Interfaces.ICartService.UpdateCartMetadataAsync"/>
/// are treated as explicit field clears rather than "leave unchanged".
/// </summary>
[Flags]
public enum CartMetadataResetFlags
{
    /// <summary>
    /// No fields are cleared; <see langword="null"/> arguments are ignored.
    /// </summary>
    None = 0,

    /// <summary>
    /// A <see langword="null"/> <c>customer</c> argument clears <see cref="Dto.CartDraft.Customer"/>.
    /// </summary>
    Customer = 1 << 0,

    /// <summary>
    /// A <see langword="null"/> <c>orderComments</c> argument clears <see cref="Dto.CartDraft.OrderComments"/>.
    /// </summary>
    OrderComments = 1 << 1,

    /// <summary>
    /// A <see langword="null"/> <c>requestedTimeSlot</c> argument clears <see cref="Dto.CartDraft.RequestedTimeSlot"/>.
    /// </summary>
    RequestedTimeSlot = 1 << 2,
}

/// <summary>
/// Describes the state of a cart as returned by a cache lookup.
/// </summary>
public enum CartLookupStatus
{
    /// <summary>
    /// The cart was found and is active.
    /// </summary>
    Found,

    /// <summary>
    /// No cart exists for the requested ID in the cache or the database.
    /// </summary>
    NotFound,

    /// <summary>
    /// The cart exists but has been disposed following submission or cancellation.
    /// Inspect <see cref="Data.DataModels.V2.Ordering.Cart.OrderConfirmation"/> to determine
    /// whether an order was created.
    /// </summary>
    Disposed
}
