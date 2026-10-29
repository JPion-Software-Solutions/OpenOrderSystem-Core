using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogProductDto(
    Guid? Id,
    string? Name,
    string? Description,
    string? Keywords,
    IReadOnlyDictionary<string, JsonElement>? Metadata,
    ICollection<CatalogVariantDto>? Variants,
    ICollection<CatalogProductOptionDto>? Options,
    CatalogMediaDto? CoverMedia,
    CatalogGroupDto? MediaAlbum,
    CatalogGroupDto? CatalogProductGroup,
    CatalogProductSnapshotDto? CurrentSnapshot = null,
    bool? IsArchived = null,
    ProductFlags? Flags = null,
    IReadOnlyDictionary<string, bool>? FlagStates = null);