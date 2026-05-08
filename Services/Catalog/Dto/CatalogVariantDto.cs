using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Catalog.Dto;

public record CatalogVariantDto(
    Guid? Id,
    Guid? GroupId,
    Guid? ProductId,
    string? Name,
    decimal? Price,
    decimal? StrikePrice,
    decimal? Cost,
    string? Sku,
    Variant.BarcodeRecord? Barcode,
    IReadOnlyDictionary<string, JsonElement>? Metadata);