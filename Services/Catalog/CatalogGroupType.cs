namespace OpenOrderSystem.Core.Services.Catalog;

/// <summary>
/// Identifies which catalog group entity a <see cref="Dto.CatalogGroupDto"/> or group operation targets.
/// All group types share the same DTO shape; this discriminator routes operations to the correct
/// underlying EF entity (<c>ProductGroup</c>, <c>OptionGroup</c>, or <c>MediaGroup</c>).
/// </summary>
public enum CatalogGroupType
{
    /// <summary>Product grouping — maps to <c>ProductGroup</c>.</summary>
    Product,

    /// <summary>Option grouping — maps to <c>OptionGroup</c>.</summary>
    Option,

    /// <summary>Media album — maps to <c>MediaGroup</c>.</summary>
    Media,
}
