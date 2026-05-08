namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogProductSnapshotDto(
    Guid? ProductId,
    Guid? PreviousSnapshotId,
    int? SchemaVersion,
    string? SnapshotJson,
    string? SnapshotHash);