using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog.Interfaces;

/// <summary>
/// Read-only projection of the product catalog. Exposes query and enrichment operations
/// without any mutating surface. Inject this interface when callers only need to read
/// catalog data; use <see cref="ICatalogManager"/> when write access is also required.
/// </summary>
public interface IReadOnlyCatalog
{
    // -------------------------
    // Products
    // -------------------------

    /// <summary>
    /// Retrieves a single product by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the product to retrieve.</param>
    /// <param name="flags">Controls which related entities are included in the result.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the matched product, or an empty result if not found.</returns>
    Task<CatalogProductQueryResult> FindProduct(Guid id, ProductLocatorFlags flags = ProductLocatorFlags.None);

    /// <summary>
    /// Retrieves a single product by name (exact match).
    /// </summary>
    /// <param name="name">The product name to search for.</param>
    /// <param name="flags">Controls which related entities are included in the result.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the matched product, or an empty result if not found.</returns>
    Task<CatalogProductQueryResult> FindProduct(string name, ProductLocatorFlags flags = ProductLocatorFlags.None);

    /// <summary>
    /// Retrieves all products belonging to the specified group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group to query.</param>
    /// <param name="flags">Controls which related entities are included and whether the group tree is traversed.
    /// See <see cref="ProductLocatorFlags.CollapseParentGroupMembers"/> and <see cref="ProductLocatorFlags.CollapseChildrenGroupMembers"/>.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the matched products.</returns>
    Task<CatalogProductQueryResult> FindProducts(Guid groupId, ProductLocatorFlags flags = ProductLocatorFlags.None);

    /// <summary>
    /// Retrieves all products belonging to the group(s) matching the specified name.
    /// </summary>
    /// <param name="groupName">The group name to search for.</param>
    /// <param name="flags">Controls which related entities are included and whether the group tree is traversed.
    /// See <see cref="ProductLocatorFlags.CollapseParentGroupMembers"/> and <see cref="ProductLocatorFlags.CollapseChildrenGroupMembers"/>.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the matched products.</returns>
    Task<CatalogProductQueryResult> FindProducts(string groupName, ProductLocatorFlags flags = ProductLocatorFlags.None);

    /// <summary>
    /// Loads additional related data onto a product DTO retrieved without full includes,
    /// avoiding a full re-fetch when only specific relations are needed.
    /// </summary>
    /// <param name="product">A previously retrieved product DTO to enrich. <see cref="CatalogProductDto.Id"/> must be set.</param>
    /// <param name="flags">Specifies which related entities to load (e.g., <see cref="ProductLocatorFlags.IncludeVariants"/>,
    /// <see cref="ProductLocatorFlags.IncludeOptions"/>).</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the enriched product.</returns>
    Task<CatalogProductQueryResult> EnrichProduct(CatalogProductDto product, ProductLocatorFlags flags = ProductLocatorFlags.None);

    // -------------------------
    // Options
    // -------------------------

    /// <summary>
    /// Retrieves a single option by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the option to retrieve.</param>
    /// <returns>A <see cref="CatalogOptionQueryResult"/> containing the matched option, or an empty result if not found.</returns>
    Task<CatalogOptionQueryResult> FindOption(Guid id);

    /// <summary>
    /// Retrieves a single option by name (exact match).
    /// </summary>
    /// <param name="name">The option name to search for.</param>
    /// <returns>A <see cref="CatalogOptionQueryResult"/> containing the matched option, or an empty result if not found.</returns>
    Task<CatalogOptionQueryResult> FindOption(string name);

    /// <summary>
    /// Retrieves all options belonging to the specified option group.
    /// </summary>
    /// <param name="groupId">The unique identifier of the option group to query.</param>
    /// <returns>A <see cref="CatalogOptionQueryResult"/> containing the matched options.</returns>
    Task<CatalogOptionQueryResult> FindOptions(Guid groupId);

    /// <summary>
    /// Retrieves all options belonging to the option group(s) matching the specified name.
    /// </summary>
    /// <param name="groupName">The option group name to search for.</param>
    /// <returns>A <see cref="CatalogOptionQueryResult"/> containing the matched options.</returns>
    Task<CatalogOptionQueryResult> FindOptions(string groupName);

    // -------------------------
    // Media
    // -------------------------

    /// <summary>
    /// Retrieves a single media item by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the media item to retrieve.</param>
    /// <returns>A <see cref="CatalogMediaQueryResult"/> containing the matched media item, or an empty result if not found.</returns>
    Task<CatalogMediaQueryResult> FindMedia(Guid id);

    /// <summary>
    /// Retrieves a single media item by name (exact match).
    /// </summary>
    /// <param name="name">The media item name to search for.</param>
    /// <returns>A <see cref="CatalogMediaQueryResult"/> containing the matched media item, or an empty result if not found.</returns>
    Task<CatalogMediaQueryResult> FindMedia(string name);

    /// <summary>
    /// Retrieves all media items belonging to the specified media group (album).
    /// </summary>
    /// <param name="groupId">The unique identifier of the media group to query.</param>
    /// <returns>A <see cref="CatalogMediaQueryResult"/> containing the matched media items.</returns>
    Task<CatalogMediaQueryResult> FindMediaInGroup(Guid groupId);

    /// <summary>
    /// Retrieves all media items belonging to the media group(s) matching the specified name.
    /// </summary>
    /// <param name="groupName">The media group name to search for.</param>
    /// <returns>A <see cref="CatalogMediaQueryResult"/> containing the matched media items.</returns>
    Task<CatalogMediaQueryResult> FindMediaInGroup(string groupName);

    // -------------------------
    // Groups
    // -------------------------

    /// <summary>
    /// Retrieves a single group by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the group to retrieve.</param>
    /// <param name="groupType">The catalog entity type this group organises.</param>
    /// <returns>A <see cref="CatalogGroupQueryResult"/> containing the matched group, or an empty result if not found.</returns>
    Task<CatalogGroupQueryResult> FindGroup(Guid id, CatalogGroupType groupType);

    /// <summary>
    /// Retrieves all groups of the specified type whose name matches the search term (exact match).
    /// </summary>
    /// <param name="name">The group name to search for.</param>
    /// <param name="groupType">The catalog entity type to restrict the search to.</param>
    /// <returns>A <see cref="CatalogGroupQueryResult"/> containing the matched groups.</returns>
    Task<CatalogGroupQueryResult> FindGroups(string name, CatalogGroupType groupType);
}
