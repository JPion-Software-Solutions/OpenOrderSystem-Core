using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

/// <summary>
/// Join entity describing how an <see cref="Option"/> behaves when it is offered on a specific <see cref="Product"/>.
/// </summary>
/// <remarks>
/// This type exists because the relationship carries behavior that is neither purely a Product concern nor purely an Option concern:
/// default selection state, and optional per-product price delta overrides.
/// <para>
/// IMPORTANT: Navigation properties are intentionally nullable to enforce explicit loading (no lazy loading).
/// </para>
/// <para>
/// Recommended invariant: there should be at most one row per (ProductId, OptionId) pair (enforce via composite PK or unique index).
/// </para>
/// </remarks>
public class ProductOption
{
    /// <summary>
    /// The owning product that offers this option.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Navigation to the owning product. Intentionally nullable to enforce explicit loading.
    /// </summary>
    public virtual Product? Product { get; set; }

    /// <summary>
    /// The option being offered by the product.
    /// </summary>
    public Guid OptionId { get; set; }

    /// <summary>
    /// Navigation to the option. Intentionally nullable to enforce explicit loading.
    /// </summary>
    public virtual Option? Option { get; set; }

    /// <summary>
    /// Indicates whether this option is selected by default when the product is chosen.
    /// </summary>
    /// <remarks>
    /// Included options are treated as "built in" and therefore do not contribute any additional price delta at selection time.
    /// </remarks>
    public ProductOptionDefaultState DefaultState { get; set; } = ProductOptionDefaultState.Optional;

    /// <summary>
    /// Optional override for <see cref="Option.PriceDelta"/> for this specific product-option relationship.
    /// </summary>
    /// <remarks>
    /// If null, the effective delta falls back to <see cref="Option.PriceDelta"/> (unless the option is <see cref="ProductOptionDefaultState.Included"/>).
    /// This enables cases like "normally +$1.00, but +$0.50 on this product" or "free add-on for this product" (override = 0).
    /// </remarks>
    public decimal? PriceDeltaOverride { get; set; }

    /// <summary>
    /// The price delta that should be applied when this option is selected for this product.
    /// </summary>
    /// <remarks>
    /// Rule order:
    /// <list type="number">
    /// <item><description>If <see cref="DefaultState"/> is <see cref="ProductOptionDefaultState.Included"/>, the option is treated as baked into the base price, so delta is 0.</description></item>
    /// <item><description>Otherwise, if <see cref="PriceDeltaOverride"/> is set, it wins.</description></item>
    /// <item><description>Otherwise, the delta comes from <see cref="Option.PriceDelta"/>.</description></item>
    /// </list>
    /// <para>
    /// This is <b>not mapped</b>. For list screens, prefer projecting an equivalent expression in LINQ to avoid requiring <see cref="Option"/> be loaded.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="Option"/> is not loaded and no override is present while computing the effective delta.
    /// </exception>
    [NotMapped]
    public decimal EffectivePriceDelta
    {
        get
        {
            if (DefaultState == ProductOptionDefaultState.Included)
                return 0m;

            if (PriceDeltaOverride.HasValue)
                return PriceDeltaOverride.Value;

            if (Option is null)
                throw new InvalidOperationException(
                    "ProductOption.Option must be loaded to compute EffectivePriceDelta when PriceDeltaOverride is null.");

            return Option.PriceDelta;
        }
    }
}

/// <summary>
/// Default selection state for an option when offered on a particular product.
/// </summary>
public enum ProductOptionDefaultState
{
    /// <summary>
    /// Option is allowed for the product, but is not selected by default.
    /// </summary>
    Optional = 0,

    /// <summary>
    /// Option is selected by default (treated as built-in to the base product/variant price).
    /// </summary>
    Included = 1
}