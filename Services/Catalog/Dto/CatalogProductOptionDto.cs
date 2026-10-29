using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

/// <summary>
/// A link between a product and a catalog option. <see cref="Option"/> and
/// <see cref="EffectivePriceDelta"/> are populated on reads and ignored on writes.
/// </summary>
public record CatalogProductOptionDto(
    Guid? ProductId,
    Guid? OptionId,
    ProductOptionDefaultState? DefaultState,
    decimal? PriceDeltaOverride,
    CatalogOptionDto? Option = null,
    decimal? EffectivePriceDelta = null);
