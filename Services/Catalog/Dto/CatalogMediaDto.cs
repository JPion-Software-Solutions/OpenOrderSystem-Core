using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogMediaDto(
    Guid? Id,
    Guid? GroupId,
    string? Name,
    string? Description,
    string? Filepath,
    string? OriginalFileName,
    string? Extension,
    string? MimeType,
    MediaType? MediaType,
    long? SizeBytes,
    string? Hash,
    IReadOnlyDictionary<string, JsonElement>? Metadata);