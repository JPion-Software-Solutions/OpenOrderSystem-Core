using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;
using OpenOrderSystem.Core.Data.Interfaces;

namespace OpenOrderSystem.Core.Data
{
    public class ApplicationDbContext : IdentityDbContext, IConfigurationStoreContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder bob)
        {
            base.OnModelCreating(bob);

            bob.Entity<Order>()
                .Property(o => o.OrderComplete)
                .HasColumnName("OrderComplete");

            bob.Entity<SystemConfig>()
                .HasKey("Key");
        }

        public Task<int> SaveChangesAsync() => base.SaveChangesAsync();

        /// <summary>
        /// Product categories used to group products by type
        /// </summary>
        public DbSet<ProductCategory> ProductCategories { get; set; }

        /// <summary>
        /// Ingredient categories used to group ingredients.
        /// </summary>
        public DbSet<IngredientCategory> IngredientCategories { get; set; }

        /// <summary>
        /// Customer information
        /// </summary>
        public DbSet<Customer> Customers { get; set; }

        /// <summary>
        /// Available Ingredients
        /// </summary>
        public DbSet<Ingredient> Ingredients { get; set; }

        /// <summary>
        /// Base menu items
        /// </summary>
        public DbSet<MenuItem> MenuItems { get; set; }

        /// <summary>
        /// Customer orders
        /// </summary>
        public DbSet<Order> Orders { get; set; }

        /// <summary>
        /// Order line items
        /// </summary>
        public DbSet<OrderLineLegacy> OrderLines { get; set; }

        /// <summary>
        /// Menu item varients
        /// </summary>
        public DbSet<MenuItemVarient> MenuItemVarients { get; set; }

        /// <summary>
        /// Confirmation codes used to confirm accounts
        /// </summary>
        public DbSet<ConfirmationCode> ConfirmationCodes { get; set; }

        /// <summary>
        /// Stores configuration settings for the system in key-value pairs.
        /// </summary>
        public DbSet<SystemConfig> Configuration { get; set; }

        public DbSet<Printer> Printers { get; set; }

        public DbSet<PrintTemplate> PrintTemplates { get; set; }

        public DbSet<BaseDiscountCode> DiscountCodes { get; set; }

        public DbSet<DiscountCodeItem> DiscountCodeItems { get; set; }

        public DbSet<MaintenanceBypassToken> MaintenanceBypassTokens { get; set; }

        /**************************************
         * Discount Code Varient Mappings
         **************************************/

        public DbSet<PercentDiscountCode> PercentDiscountCodes { get; set; }
        public DbSet<FixedAmountDiscountCode> FixedAmountDiscountCodes { get; set; }
        public DbSet<BuyXGetXForYDiscountCode> BuyXGetXForYDiscountCodes { get; set; }
        public DbSet<BuyXGetYForZDiscountCode> BuyXGetYForZDiscountCodes { get; set; }
    }
}
