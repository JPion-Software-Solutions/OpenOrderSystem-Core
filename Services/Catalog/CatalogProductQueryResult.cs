using OpenOrderSystem.Core.Services.Catalog.Dto;
using OpenOrderSystem.Core.Services.Catalog.Interfaces;

namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Result type for catalog product query operations. Extends <see cref="CatalogResult"/>
/// with a list of matched products. Single-item finders return a list of 0 or 1 entries;
/// use <see cref="IReadOnlyList{T}.Count"/> or <c>.SingleOrDefault()</c> to unwrap.
/// </summary>
public class CatalogProductQueryResult : CatalogResult
{
    /// <summary>
    /// The products returned by the query. Empty when no matches were found or the operation failed.
    /// </summary>
    public IReadOnlyList<CatalogProductDto> Results { get; init; } = [];
    
    /// <summary>
    /// The <see cref="ProductLocatorFlags"/> that were active when this query was executed.
    /// Allows callers to determine which related entities are present in <see cref="Results"/>
    /// without tracking the flags separately.
    /// </summary>
    public ProductLocatorFlags WithFlags { get; init; } = ProductLocatorFlags.None;
}
