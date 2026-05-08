using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Result type for catalog media query operations. Extends <see cref="CatalogResult"/>
/// with a list of matched media items. Single-item finders return a list of 0 or 1 entries;
/// use <see cref="IReadOnlyList{T}.Count"/> or <c>.SingleOrDefault()</c> to unwrap.
/// </summary>
public class CatalogMediaQueryResult : CatalogResult
{
    /// <summary>
    /// The media items returned by the query. Empty when no matches were found or the operation failed.
    /// </summary>
    public IReadOnlyList<CatalogMediaDto> Results { get; init; } = [];
}
