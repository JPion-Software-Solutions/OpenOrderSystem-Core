using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Ordering;

/// <summary>
/// Represents a persisted customer cart awaiting submission as an order.
/// </summary>
/// <remarks>
/// <para>
/// The cart is intentionally thin. All serialization and business logic is owned by the
/// cart service, which maintains recently active carts in memory to avoid repeated
/// deserialize/serialize round-trips on rapid mutations (add, remove, update item).
/// On each mutation the service updates <see cref="DraftOrder"/> and <see cref="LastActive"/>
/// and persists them to the database, so the cart survives process restarts without data loss.
/// </para>
/// <para>
/// When a cart is not present in the service's in-memory cache (cold start or eviction),
/// it is hydrated from <see cref="DraftOrder"/>. Eviction is driven by <see cref="LastActive"/>
/// against a configured idle TTL.
/// </para>
/// <para>
/// On submission the cart service projects the draft into an <see cref="OrderHeader"/>,
/// assigns an <see cref="OrderConfirmation"/> number, and marks the cart disposed via
/// <see cref="IsDisposed"/>. The confirmation number is retained on the cart record to
/// support idempotency checks during the short post-submission window before the GC purges it.
/// </para>
/// <para>
/// The cart garbage collector applies two TTL conditions independently:
/// <list type="bullet">
/// <item><description><see cref="IsDisposed"/> <see langword="= true"/> — purged after a short TTL, long enough for idempotency checks.</description></item>
/// <item><description><see cref="IsDisposed"/> <see langword="= false"/> and <see cref="LastActive"/> older than the abandoned cart threshold — purged as abandoned.</description></item>
/// </list>
/// </para>
/// </remarks>
public class Cart
{
    /// <summary>
    /// Unique identifier for this cart. Used as the cache key in the cart service's
    /// in-memory store and as the session reference returned to the client.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// JSON-serialized draft order state managed exclusively by the cart service.
    /// </summary>
    /// <remarks>
    /// Do not read or write this property directly. Use the cart service to interact
    /// with cart contents. This column is updated on every mutation so that the
    /// persisted state always reflects the latest in-memory draft.
    /// </remarks>
    public string DraftOrder { get; set; } = string.Empty;

    /// <summary>
    /// The order confirmation number assigned at submission time, or <see langword="null"/>
    /// if the cart has not yet been submitted.
    /// </summary>
    /// <remarks>
    /// Confirmation numbers are 8-digit integers encoding a day counter and a daily-resetting
    /// order sequence in the format <c>xxxxxyyy</c>, mapped to a non-sequential value via a
    /// reversible transformation. This is the primary customer-facing order lookup key.
    /// On a duplicate submission request, a non-null value here indicates the order was already
    /// created and the stored confirmation number can be returned directly.
    /// </remarks>
    public int? OrderConfirmation { get; set; } = null;

    /// <summary>
    /// The date and time this cart was created (UTC).
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The date and time of the most recent mutation to this cart (UTC).
    /// Used by the cart service to drive in-memory cache eviction and by the
    /// cleanup job to purge abandoned carts past the configured idle TTL.
    /// </summary>
    public DateTimeOffset LastActive { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Signals the cart garbage collector to purge this cart after the short disposed TTL,
    /// rather than the normal abandoned cart threshold.
    /// </summary>
    /// <remarks>
    /// Set by the cart service on submission or explicit cancellation. The shorter TTL
    /// allows idempotency checks to succeed during the post-submission window while
    /// still ensuring timely cleanup.
    /// </remarks>
    public bool IsDisposed { get; set; } = false;
}