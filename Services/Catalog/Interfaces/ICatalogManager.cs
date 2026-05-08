using OpenOrderSystem.Core.Services.Catalog.Dto;

namespace OpenOrderSystem.Core.Services.Catalog.Interfaces;

/// <summary>
/// Full read/write surface for the product catalog. Extends <see cref="IReadOnlyCatalog"/>
/// with mutating operations. Inject <see cref="IReadOnlyCatalog"/> when write access is not
/// required; use this interface when callers need to create, update, or delete catalog entities.
/// A single concrete implementation satisfies both interfaces.
/// </summary>
public interface ICatalogManager : IReadOnlyCatalog
{
    // -------------------------
    // Products
    // -------------------------

    /// <summary>
    /// Creates a new product in the catalog from the supplied DTO.
    /// </summary>
    /// <param name="product">The product data to persist. <see cref="CatalogProductDto.Id"/> is ignored if set;
    /// a new identifier is assigned by the store.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the newly created product, or a failure result.</returns>
    Task<CatalogProductQueryResult> AddProduct(CatalogProductDto product);

    /// <summary>
    /// Updates an existing product using the supplied DTO. Null fields are left unchanged.
    /// </summary>
    /// <param name="product">The product data to apply. <see cref="CatalogProductDto.Id"/> must be set
    /// to identify the target record.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the updated product, or a failure result.</returns>
    Task<CatalogProductQueryResult> UpdateProduct(CatalogProductDto product);

    /// <summary>
    /// Deletes the product identified by <see cref="CatalogProductDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="product">DTO whose <see cref="CatalogProductDto.Id"/> identifies the product to delete.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the deleted product as it existed before removal, or a failure result.</returns>
    Task<CatalogProductQueryResult> DeleteProduct(CatalogProductDto product);

    /// <summary>
    /// Deletes the product with the specified identifier.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to delete.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the deleted product as it existed before removal, or a failure result.</returns>
    Task<CatalogProductQueryResult> DeleteProduct(Guid productId);

    // -------------------------
    // Options
    // -------------------------

    /// <summary>
    /// Creates a new option in the catalog from the supplied DTO.
    /// </summary>
    /// <param name="option">The option data to persist. <see cref="CatalogOptionDto.Id"/> is ignored if set;
    /// a new identifier is assigned by the store.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> AddOption(CatalogOptionDto option);

    /// <summary>
    /// Updates an existing option using the supplied DTO. Null fields are left unchanged.
    /// </summary>
    /// <param name="option">The option data to apply. <see cref="CatalogOptionDto.Id"/> must be set
    /// to identify the target record.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> UpdateOption(CatalogOptionDto option);

    /// <summary>
    /// Deletes the option identified by <see cref="CatalogOptionDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="option">DTO whose <see cref="CatalogOptionDto.Id"/> identifies the option to delete.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteOption(CatalogOptionDto option);

    /// <summary>
    /// Deletes the option with the specified identifier.
    /// </summary>
    /// <param name="optionId">The unique identifier of the option to delete.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteOption(Guid optionId);

    // -------------------------
    // Media
    // -------------------------

    /// <summary>
    /// Registers a new media item in the catalog from the supplied DTO.
    /// </summary>
    /// <param name="media">The media data to persist. <see cref="CatalogMediaDto.Id"/> is ignored if set;
    /// a new identifier is assigned by the store.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> AddMedia(CatalogMediaDto media);

    /// <summary>
    /// Updates an existing media item using the supplied DTO. Null fields are left unchanged.
    /// </summary>
    /// <param name="media">The media data to apply. <see cref="CatalogMediaDto.Id"/> must be set
    /// to identify the target record.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> UpdateMedia(CatalogMediaDto media);

    /// <summary>
    /// Deletes the media item identified by <see cref="CatalogMediaDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="media">DTO whose <see cref="CatalogMediaDto.Id"/> identifies the media item to delete.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteMedia(CatalogMediaDto media);

    /// <summary>
    /// Deletes the media item with the specified identifier.
    /// </summary>
    /// <param name="mediaId">The unique identifier of the media item to delete.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteMedia(Guid mediaId);

    // -------------------------
    // Groups
    // -------------------------

    /// <summary>
    /// Creates a new group of the specified type from the supplied DTO.
    /// </summary>
    /// <param name="group">The group data to persist. <see cref="CatalogGroupDto.Id"/> is ignored if set;
    /// a new identifier is assigned by the store.</param>
    /// <param name="groupType">The catalog entity type this group will organise.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> AddGroup(CatalogGroupDto group, CatalogGroupType groupType);

    /// <summary>
    /// Updates an existing group using the supplied DTO. Null fields are left unchanged.
    /// </summary>
    /// <param name="group">The group data to apply. <see cref="CatalogGroupDto.Id"/> must be set
    /// to identify the target record.</param>
    /// <param name="groupType">The catalog entity type of the group being updated.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> UpdateGroup(CatalogGroupDto group, CatalogGroupType groupType);

    /// <summary>
    /// Deletes the group identified by <see cref="CatalogGroupDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="group">DTO whose <see cref="CatalogGroupDto.Id"/> identifies the group to delete.</param>
    /// <param name="groupType">The catalog entity type of the group being deleted.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteGroup(CatalogGroupDto group, CatalogGroupType groupType);

    /// <summary>
    /// Deletes the group with the specified identifier.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group to delete.</param>
    /// <param name="groupType">The catalog entity type of the group being deleted.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> DeleteGroup(Guid groupId, CatalogGroupType groupType);

    // -------------------------
    // Variant pricing (bulk)
    // -------------------------

    /// <summary>
    /// Applies a uniform price and optional strike-price to all variants within the specified group.
    /// Targets the group by its unique identifier. Pass <see langword="null"/> for either price
    /// to leave that field unchanged on all members.
    /// </summary>
    /// <param name="variantGroupId">The unique identifier of the variant group whose members will be updated.</param>
    /// <param name="price">The new selling price to apply, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="strikePrice">The new strike-through price to apply, or <see langword="null"/> to leave unchanged.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> UpdateVariantGroupPricing(Guid variantGroupId, decimal? price, decimal? strikePrice);

    /// <summary>
    /// Applies a uniform price and optional strike-price to all variants within the group(s)
    /// matching the specified name. Pass <see langword="null"/> for either price to leave
    /// that field unchanged on all members.
    /// </summary>
    /// <param name="variantGroupName">The name of the variant group(s) whose members will be updated.</param>
    /// <param name="price">The new selling price to apply, or <see langword="null"/> to leave unchanged.</param>
    /// <param name="strikePrice">The new strike-through price to apply, or <see langword="null"/> to leave unchanged.</param>
    /// <returns>A <see cref="CatalogResult"/> indicating success or failure.</returns>
    Task<CatalogResult> UpdateVariantGroupPricing(string variantGroupName, decimal? price, decimal? strikePrice);
}
