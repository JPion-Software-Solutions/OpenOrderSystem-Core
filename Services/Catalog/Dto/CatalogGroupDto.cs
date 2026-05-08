namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogGroupDto(
    Guid? Id,
    Guid? ParentId,
    string? Name,
    string? Description,
    int? SortPriority);