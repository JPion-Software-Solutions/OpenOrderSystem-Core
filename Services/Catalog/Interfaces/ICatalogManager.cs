using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;
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
    /// Creates a new product in the catalog from the supplied DTO, along with any supplied
    /// variants and option links. Variants and option links follow the same rules as
    /// <see cref="UpdateProduct"/>; on create, a variant entry must not carry an <see cref="CatalogVariantDto.Id"/>.
    /// </summary>
    /// <param name="product">The product data to persist. <see cref="CatalogProductDto.Id"/> is ignored if set;
    /// a new identifier is assigned by the store.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the newly created product with its
    /// variants and option links, or a failure result.</returns>
    Task<CatalogProductQueryResult> AddProduct(CatalogProductDto product);

    /// <summary>
    /// Updates an existing product using the supplied DTO. Null product fields are left unchanged.
    /// </summary>
    /// <remarks>
    /// <para><see cref="CatalogProductDto.Variants"/> and <see cref="CatalogProductDto.Options"/> are
    /// left untouched when <see langword="null"/>. When supplied (even empty), each collection is treated
    /// as the complete set for the product:</para>
    /// <list type="bullet">
    /// <item><description>Variants with an <see cref="CatalogVariantDto.Id"/> belonging to this product are updated;
    /// variants without an Id are created; existing variants absent from the set are deleted. An Id that does
    /// not belong to this product fails the whole update.</description></item>
    /// <item><description>Each variant entry is its <b>full state</b>: a <see langword="null"/>
    /// <see cref="CatalogVariantDto.StrikePrice"/>, <see cref="CatalogVariantDto.Cost"/>,
    /// <see cref="CatalogVariantDto.Sku"/>, <see cref="CatalogVariantDto.Barcode"/>,
    /// <see cref="CatalogVariantDto.GroupId"/>, or <see cref="CatalogVariantDto.Metadata"/> clears that value.
    /// Send back the complete variant, not a partial patch.</description></item>
    /// <item><description>Option links are matched by <see cref="CatalogProductOptionDto.OptionId"/>; links absent
    /// from the set are removed. The options themselves are never modified. Each link entry is its full state
    /// (<see langword="null"/> <see cref="CatalogProductOptionDto.DefaultState"/> means
    /// <c>Optional</c>, <see langword="null"/> <see cref="CatalogProductOptionDto.PriceDeltaOverride"/> clears it).</description></item>
    /// </list>
    /// <para>All changes are validated before anything is written and are saved together with a single snapshot.</para>
    /// </remarks>
    /// <param name="product">The product data to apply. <see cref="CatalogProductDto.Id"/> must be set
    /// to identify the target record.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the updated product with its variants and
    /// option links, or a failure result.</returns>
    Task<CatalogProductQueryResult> UpdateProduct(CatalogProductDto product);

    /// <summary>
    /// Archives the product identified by <see cref="CatalogProductDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="product">DTO whose <see cref="CatalogProductDto.Id"/> identifies the product to archive.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the archived product, or a failure result.</returns>
    Task<CatalogProductQueryResult> ArchiveProduct(CatalogProductDto product);

    /// <summary>
    /// Archives the product with the specified identifier. Products are never deleted: an archived
    /// product, with its variants, option links and snapshots, stays in the database so order history
    /// remains intact. It is excluded from every read unless <see cref="ProductLocatorFlags.IncludeArchived"/>
    /// is set. Archiving an already-archived product succeeds without change. No snapshot is written.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to archive.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the archived product, or a failure result.</returns>
    Task<CatalogProductQueryResult> ArchiveProduct(Guid productId);

    /// <summary>
    /// Restores the archived product identified by <see cref="CatalogProductDto.Id"/> in the supplied DTO.
    /// </summary>
    /// <param name="product">DTO whose <see cref="CatalogProductDto.Id"/> identifies the product to restore.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the restored product, or a failure result.</returns>
    Task<CatalogProductQueryResult> RestoreProduct(CatalogProductDto product);

    /// <summary>
    /// Restores an archived product so it appears in normal reads again. Restoring a product that
    /// is not archived succeeds without change.
    /// </summary>
    /// <param name="productId">The unique identifier of the product to restore.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the restored product, or a failure result.</returns>
    Task<CatalogProductQueryResult> RestoreProduct(Guid productId);

    /// <summary>
    /// Sets and/or clears <see cref="ProductFlags"/> bits on a product. Bits named in neither mask are left
    /// as they are, so independent callers (operators toggling availability, the promotion service setting
    /// <see cref="ProductFlags.IsPromoted"/>) don't overwrite each other's flags.
    /// </summary>
    /// <remarks>
    /// Flags are operational state rather than product content, so no snapshot is written. Works on
    /// archived products. Product edits made through <see cref="UpdateProduct"/> never change flags.
    /// </remarks>
    /// <param name="productId">The unique identifier of the product.</param>
    /// <param name="set">Bits to turn on.</param>
    /// <param name="clear">Bits to turn off. Must not overlap <paramref name="set"/>.</param>
    /// <returns>A <see cref="CatalogProductQueryResult"/> containing the product with its resulting flags, or a failure result.</returns>
    Task<CatalogProductQueryResult> UpdateProductFlags(Guid productId, ProductFlags set = ProductFlags.None, ProductFlags clear = ProductFlags.None);

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
