using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;

namespace OpenOrderSystem.Core.Data
{
    public class ApplicationDbContext : IdentityDbContext
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
        }

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
        public DbSet<OrderLine> OrderLines { get; set; }

        /// <summary>
        /// Menu item varients
        /// </summary>
        public DbSet<MenuItemVarient> MenuItemVarients { get; set; }

        /// <summary>
        /// Confirmation codes used to confirm accounts
        /// </summary>
        public DbSet<ConfirmationCode> ConfirmationCodes { get; set; }

        public DbSet<Printer> Printers { get; set; }

        public DbSet<PrintTemplate> PrintTemplates { get; set; }

        public DbSet<BaseDiscountCode> DiscountCodes { get; set; }

        public DbSet<DiscountCodeItem> DiscountCodeItems { get; set; }

        /**************************************
         * Discount Code Varient Mappings
         **************************************/

        public DbSet<PercentDiscountCode> PercentDiscountCodes { get; set; }
        public DbSet<FixedAmountDiscountCode> FixedAmountDiscountCodes { get; set; }
        public DbSet<BuyXGetXForYDiscountCode> BuyXGetXForYDiscountCodes { get; set; }
        public DbSet<BuyXGetYForZDiscountCode> BuyXGetYForZDiscountCodes { get; set; }
    }
}
