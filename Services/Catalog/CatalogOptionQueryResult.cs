using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Result type for catalog option query operations. Extends <see cref="CatalogResult"/>
/// with a list of matched options. Single-item finders return a list of 0 or 1 entries;
/// use <see cref="IReadOnlyList{T}.Count"/> or <c>.SingleOrDefault()</c> to unwrap.
/// </summary>
public class CatalogOptionQueryResult : CatalogResult
{
    /// <summary>
    /// The options returned by the query. Empty when no matches were found or the operation failed.
    /// </summary>
    public IReadOnlyList<CatalogOptionDto> Results { get; init; } = [];
}
