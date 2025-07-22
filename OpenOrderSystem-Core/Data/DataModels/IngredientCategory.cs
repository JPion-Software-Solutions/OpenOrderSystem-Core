using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class IngredientCategory
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name associated with ingredient category
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
        /// Type of category. (exclusive or multiple)
        /// </summary>
        public IngredientType Type { get; set; }

        /// <summary>
        /// Ingredients that belong to this category.
        /// </summary>
        public List<Ingredient>? MemberIngredients { get; set; }
    }

    public enum IngredientType
    {
        /// <summary>
        /// Allows user to pick only one ingredient in the category
        /// </summary>
        Exclusive,

        /// <summary>
        /// Allows the user to pick any ingredient in the category
        /// </summary>
        Multiple
    }
}
