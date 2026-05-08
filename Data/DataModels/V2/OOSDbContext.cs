using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2.Catalog;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;
using OpenOrderSystem.Core.Data.DataModels.V2.Devices;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Data.Interfaces;
using OpenOrderSystem.Core.Services.Catalog.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;

namespace OpenOrderSystem.Core.Data.DataModels.V2;

public class OosDbContext : IdentityDbContext, IConfigurationStoreContext, ICatalogDataContext
{
    // ----------------------------
    // Core Models
    // ----------------------------
    public DbSet<SystemConfig> Configuration { get; set; }

    public DbSet<MaintenanceBypassToken> MaintenanceBypassTokens { get; set; }

    // -----------------------------
    // Catalog (V2)
    // -----------------------------

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

    // -----------------------------
    // Devices (V2)
    // -----------------------------

    /// <summary>
    /// Registered devices and integration endpoints in the OOS device registry.
    /// </summary>
    public DbSet<DeviceHead> Devices { get; set; }
    
    
    // -----------------------------
    // Ordering (V2)
    // -----------------------------
    
    /// <summary>
    /// The configured stages that define the order fulfillment workflow.
    /// </summary>
    public DbSet<OrderStage> OrderStages { get; set; }

    /// <summary>
    /// Order header records tracking customer orders through the fulfillment workflow.
    /// </summary>
    public DbSet<OrderHeader> Orders { get; set; }

    /// <summary>
    /// Individual line items belonging to customer orders.
    /// </summary>
    public DbSet<OrderLine> OrderLines { get; set; }

    /// <summary>
    /// Customer records associated with orders, subject to the configured data retention policy.
    /// </summary>
    public DbSet<CustomerRecord> CustomerRecords { get; set; }
    
    /// <summary>
    /// Orders currently queued for future fulfillment, awaiting promotion into the active stage chain.
    /// </summary>
    public DbSet<OrderQueue> OrderQueue { get; set; }
    
    public OosDbContext(DbContextOptions<OosDbContext> options) : base(options) { }

    public Task<int> SaveChangesAsync() => base.SaveChangesAsync();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<decimal?>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // DeviceConfig composite PK
        modelBuilder.Entity<DeviceConfig>()
            .HasKey(x => new { x.DeviceId, x.Key });

        // DeviceHead unique index on Key
        modelBuilder.Entity<DeviceHead>()
            .HasIndex(d => d.Key)
            .IsUnique()
            .HasDatabaseName("UIX_DeviceHead_Key");
        
        // OrderHeader indexes
        modelBuilder.Entity<OrderHeader>()
            .HasIndex(o => o.StageAdvanceTime)
            .HasDatabaseName("IX_OrderHeader_StageAdvanceTime");

        modelBuilder.Entity<OrderHeader>()
            .HasIndex(o => o.AssignedTimeSlot)
            .HasDatabaseName("IX_OrderHeader_AssignedTimeSlot");

        // OrderQueue indexes
        modelBuilder.Entity<OrderQueue>()
            .HasIndex(q => q.ScheduledFor)
            .HasDatabaseName("IX_OrderQueue_ScheduledFor");

        modelBuilder.Entity<OrderHeader>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("UIX_OrderHeader_OrderNumber");

        modelBuilder.Entity<ProductOption>()
            .HasKey(x => new { x.ProductId, x.OptionId });

        modelBuilder.Entity<OrderHeader>().OwnsMany(o => o.StageHistory);

        // OrderStage is a doubly-linked list — two independent self-referencing one-to-one FKs.
        modelBuilder.Entity<OrderStage>()
            .HasOne(s => s.NextStage)
            .WithOne()
            .HasForeignKey<OrderStage>(s => s.NextStageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderStage>()
            .HasOne(s => s.PreviousStage)
            .WithOne()
            .HasForeignKey<OrderStage>(s => s.PreviousStageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
