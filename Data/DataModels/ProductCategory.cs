using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class ProductCategory
    {
        /// <summary>
        /// Table Id column
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the product category (ex: Pizza or Cheese Bread)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Normailized version of the name safe for use in HTML as id's or classes
        /// </summary>
        [NotMapped]
        public string NormailizedName
        {
            get => Name.ToLower().Replace(" ", "_");
        }

        /// <summary>
        /// Gives a value to sort item by. Higher number, further up the page. Negative number, item is hidden
        /// </summary>
        public int Priority { get; set; } = 0;

        [NotMapped]
        public virtual bool IsArchived => Priority <= 0;

        /// <summary>
        /// Product category description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Ingredients allowed in the product category
        /// </summary>
        public List<Ingredient>? Ingredients { get; set; }

        /// <summary>
        /// Menuitems in the product category
        /// </summary>
        public List<MenuItem>? MenuItems { get; set; }
    }
}
