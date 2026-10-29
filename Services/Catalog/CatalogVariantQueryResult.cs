using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Result type for catalog variant query operations. Extends <see cref="CatalogResult"/>
/// with a list of matched variants.
/// </summary>
public class CatalogVariantQueryResult : CatalogResult
{
    /// <summary>
    /// The variants returned by the query. Empty when no matches were found or the operation failed.
    /// </summary>
    public IReadOnlyList<CatalogVariantDto> Results { get; init; } = [];
}
