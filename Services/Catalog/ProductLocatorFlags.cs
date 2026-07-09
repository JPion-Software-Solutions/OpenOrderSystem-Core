namespace OpenOrderSystem.Core.Services.Catalog;

[Flags]
public enum ProductLocatorFlags
{
    None = 0,

    /// <summary>
    /// Include associated Product.Options when retrieving products.
    /// </summary>
    IncludeOptions               = 1 << 0,

    /// <summary>
    /// Include associated Product.Variants when retrieving products.
    /// </summary>
    IncludeVariants              = 1 << 1,

    /// <summary>
    /// Include associated Product.Media when retrieving products.
    /// </summary>
    IncludeMedia                 = 1 << 2,

    /// <summary>
    /// Include associated Metadata when retrieving records.
    /// </summary>
    IncludeMetadata              = 1 << 3,

    /// <summary>
    /// Include most recent snapshot when retrieving records.
    /// </summary>
    IncludeSnapshot              = 1 << 4,

    /// <summary>
    /// Include all products within the queried group(s) as well as any products in their parent group(s).
    /// </summary>
    CollapseParentGroupMembers   = 1 << 10,

    /// <summary>
    /// Include all products within the queried group(s) as well as any products in their child/descendant group(s).
    /// Flattens the entire subtree rooted at the queried group into the result.
    /// </summary>
    CollapseChildrenGroupMembers = 1 << 11,
}