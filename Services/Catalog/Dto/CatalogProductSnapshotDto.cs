namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogProductSnapshotDto(
    Guid? Id,
    Guid? ProductId,
    Guid? PreviousSnapshotId,
    int? SchemaVersion,
    string? SnapshotJson,
    string? SnapshotHash);