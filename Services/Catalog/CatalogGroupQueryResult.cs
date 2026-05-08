using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Result type for catalog group query operations. Extends <see cref="CatalogResult"/>
/// with a list of matched groups and the <see cref="CatalogGroupType"/> that was queried.
/// Single-item finders return a list of 0 or 1 entries;
/// use <see cref="IReadOnlyList{T}.Count"/> or <c>.SingleOrDefault()</c> to unwrap.
/// </summary>
public class CatalogGroupQueryResult : CatalogResult
{
    /// <summary>
    /// The groups returned by the query. Empty when no matches were found or the operation failed.
    /// </summary>
    public IReadOnlyList<CatalogGroupDto> Results { get; init; } = [];

    /// <summary>
    /// The <see cref="CatalogGroupType"/> that was active when this query was executed.
    /// Allows callers to determine which group entity type is represented in <see cref="Results"/>
    /// without tracking the type separately.
    /// </summary>
    public CatalogGroupType GroupType { get; init; }
}
