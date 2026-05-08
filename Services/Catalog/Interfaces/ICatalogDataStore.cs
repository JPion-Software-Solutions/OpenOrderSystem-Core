using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;
using OpenOrderSystem.Core.Services.Catalog;
using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog.Interfaces;

/// <summary>
/// Provides the data access layer for the product catalog, exposing CRUD and query operations
/// over catalog entities. Higher-level catalog services use this store as their sole data access mechanism,
/// keeping direct EF/database dependencies out of service logic.
/// </summary>
public interface ICatalogDataStore
{
    /// <summary>
    /// Creates a new product in the catalog from the supplied DTO.
    /// </summary>
    /// <param name="product">The product data to persist. <see cref="CatalogProductDto.Id"/> is ignored if set;
    /// a new identifier is assigned by the store.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> AddProduct(CatalogProductDto product);

    /// <summary>
    /// Updates an existing product using the supplied DTO. Null fields are left unchanged.
    /// </summary>
    /// <param name="product">The product data to apply. <see cref="CatalogProductDto.Id"/> must be set
    /// to identify the target record.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> UpdateProduct(CatalogProductDto product);

    /// <summary>
    /// Deletes the product identified by <see cref="CatalogProductDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="product">DTO whose <see cref="CatalogProductDto.Id"/> identifies the product to delete.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteProduct(CatalogProductDto product);

    /// <summary>
    /// Deletes the product with the specified identifier.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to delete.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteProduct(Guid productId);

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
}

[Flags]
public enum ProductLocatorFlags
{
    None = 0,

    /// <summary>
    /// Include associated Product.Options when retrieving products
    /// </summary>
    IncludeOptions                  = 1 << 0,

    /// <summary>
    /// Include associated Product.Variants when retrieving products
    /// </summary>
    IncludeVariants                 = 1 << 1,

    /// <summary>
    /// Include associated Product.Media when retrieving products 
    /// </summary>
    IncludeMedia                    = 1 << 2,
    
    /// <summary>
    /// Include associated Metadata when retrieving records 
    /// </summary>
    IncludeMetadata                    = 1 << 3,

    /// <summary>
    /// Include all products within the queried group(s) as well as any products in their parent group(s).
    /// </summary>
    CollapseParentGroupMembers      = 1 << 10,

    /// <summary>
    /// Include all products within the queried group(s) as well as any products in their child/descendant group(s).
    /// Flattens the entire subtree rooted at the queried group into the result.
    /// </summary>
    CollapseChildrenGroupMembers    = 1 << 11,
}