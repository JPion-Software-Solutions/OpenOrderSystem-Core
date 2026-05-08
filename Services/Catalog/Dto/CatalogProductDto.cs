using System.Text.Json;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogProductDto(
    Guid? Id,
    string? Name,
    string? Description,
    string? Keywords,
    IReadOnlyDictionary<string, JsonElement>? Metadata,
    ICollection<CatalogVariantDto>? Variants,
    ICollection<CatalogOptionDto>? Options,
    CatalogMediaDto? CoverMedia,
    CatalogGroupDto? MediaAlbum,
    CatalogGroupDto? CatalogProductGroup);