using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;
using OpenOrderSystem.Core.Data.DataModels.V2.Devices;
using OpenOrderSystem.Core.Data.Interfaces;

namespace OpenOrderSystem.Core.Data.DataModels.V2;

public class OOSDbContext : IdentityDbContext, IConfigurationStoreContext
{
    // ----------------------------
    // Core Models
    // ----------------------------
    public DbSet<SystemConfig> Configuration { get; set; }

    public DbSet<MaintenanceBypassToken> MaintenanceBypassTokens { get; set; }

    public DbSet<Device> Devices { get; set; }

    // -----------------------------
    // Catalog (V2)
    // -----------------------------

    /// <summary>
    /// Live product catalog entries (current state).
    /// </summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Hierarchical grouping for products (category tree).
    /// </summary>
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();

    /// <summary>
    /// Purchasable variations of products (e.g., size, package, configuration).
    /// </summary>
    public DbSet<Variant> Variants => Set<Variant>();

    /// <summary>
    /// Hierarchical grouping for variants (optional taxonomy).
    /// </summary>
    public DbSet<VariantGroup> VariantGroups => Set<VariantGroup>();

    /// <summary>
    /// Selectable options/add-ons that can be attached to products/variants.
    /// </summary>
    public DbSet<Option> Options => Set<Option>();

    /// <summary>
    /// Hierarchical grouping for options (e.g., "Sauces", "Toppings", "Extras").
    /// </summary>
    public DbSet<OptionGroup> OptionGroups => Set<OptionGroup>();

    /// <summary>
    /// Join table defining which options are available/included for a given product,
    /// including default state and optional overrides.
    /// </summary>
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();

    /// <summary>
    /// Media assets tracked by the catalog (images, audio, video, files).
    /// </summary>
    public DbSet<Media> Media => Set<Media>();

    /// <summary>
    /// Media collections/albums (grouping) for catalog usage.
    /// </summary>
    public DbSet<MediaGroup> MediaGroups => Set<MediaGroup>();

    /// <summary>
    /// Immutable historical snapshots of a product graph (product + variants/options/media) at a point in time.
    /// Used for audit/history and order stability.
    /// </summary>
    public DbSet<ProductSnapshot> ProductSnapshots => Set<ProductSnapshot>();

    public OOSDbContext(DbContextOptions<OOSDbContext> options) : base(options) { }

    public Task<int> SaveChangesAsync() => base.SaveChangesAsync();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<decimal?>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProductOption>()
            .HasKey(x => new { x.ProductId, x.OptionId });
    }
}
