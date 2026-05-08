using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogOptionDto(
    Guid? Id,
    Guid? GroupId,
    string? Name,
    decimal? PriceDelta,
    OptionFlags? Flags);