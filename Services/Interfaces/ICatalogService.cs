using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;

namespace OpenOrderSystem.Core.Services.Interfaces;

public interface ICatalogDataStore<TDataContext>
where TDataContext: DbContext, ICatalogDataContext
{
    /// <summary>
    /// Query a group or groups by name and return their associated product(s)
    /// </summary>
    /// <param name="group">Single group name or comma-separated list of groups</param>
    /// <param name="flags">Modifies rules on group tree traversing</param>
    /// <returns>Collection of products associated with the group(s)</returns>
    ICollection<Product> GetProductsByGroupName(string group, ProductLocatorFlags flags = ProductLocatorFlags.None);
}

public interface ICatalogDataContext
{
    /// <summary>
    /// Live product catalog entries (current state).
    /// </summary>
    public DbSet<Product> Products { get; set; }

    /// <summary>
    /// Hierarchical grouping for products (category tree).
    /// </summary>
    public DbSet<ProductGroup> ProductGroups  { get; set; }

    /// <summary>
    /// Purchasable variations of products (e.g., size, package, configuration).
    /// </summary>
    public DbSet<Variant> Variants  { get; set; }

    /// <summary>
    /// Hierarchical grouping for variants (optional taxonomy).
    /// </summary>
    public DbSet<VariantGroup> VariantGroups  { get; set; }

    /// <summary>
    /// Selectable options/add-ons that can be attached to products/variants.
    /// </summary>
    public DbSet<Option> Options  { get; set; }

    /// <summary>
    /// Hierarchical grouping for options (e.g., "Sauces", "Toppings", "Extras").
    /// </summary>
    public DbSet<OptionGroup> OptionGroups  { get; set; }

    /// <summary>
    /// Join table defining which options are available/included for a given product,
    /// including default state and optional overrides.
    /// </summary>
    public DbSet<ProductOption> ProductOptions  { get; set; }

    /// <summary>
    /// Media assets tracked by the catalog (images, audio, video, files).
    /// </summary>
    public DbSet<Media> Media { get; set; }

    /// <summary>
    /// Media collections/albums (grouping) for catalog usage.
    /// </summary>
    public DbSet<MediaGroup> MediaGroups  { get; set; }

    /// <summary>
    /// Immutable historical snapshots of a product graph (product + variants/options/media) at a point in time.
    /// Used for audit/history and order stability.
    /// </summary>
    public DbSet<ProductSnapshot> ProductSnapshots  { get; set; }
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
    /// Include all products within the queried group(s) as well as any products in their parent group(s).
    /// </summary>
    CollapseParentGroupMembers      = 1 << 3,

    /// <summary>
    /// 
    /// </summary>
    CollapseChildrenGroupMembers    = 1 << 4,
}