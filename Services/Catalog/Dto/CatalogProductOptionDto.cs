using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogProductOptionDto(
    Guid? ProductId,
    Guid? OptionId,
    ProductOptionDefaultState? DefaultState,
    decimal? PriceDeltaOverride);