using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using System.Web.Helpers;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class OrderLine
    {
        /// <summary>
        /// Primary key
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Foriegn key of base menu item
        /// </summary>
        public int MenuItemId { get; set; }

        /// <summary>
        /// Nav propert for base menu item
        /// </summary>
        public MenuItem? MenuItem { get; set; }

        /// <summary>
        /// Foriegn key for order
        /// </summary>
        public int OrderId { get; set; }

        /// <summary>
        /// Nav property for order
        /// </summary>
        public Order? Order { get; set; }

        /// <summary>
        /// Varient of the menu item (defaults to 0/first varient)
        /// </summary>
        public int MenuItemVarient { get; set; }

        /// <summary>
        /// Any comments left by the customer for this line will appear here
        /// </summary>
        [MaxLength(128)]
        public string? LineComments { get; set; }

        /// <summary>
        /// Navigation property to the ingredients included in this line item
        /// </summary>
        public List<Ingredient>? Ingredients { get; set; }

        /// <summary>
        /// All ingredients that have been added to the base item
        /// </summary>
        [NotMapped]
        public List<Ingredient> AddedIngredients
        {
            get
            {
                var added = new List<Ingredient>();

                if (Ingredients == null)
                    return added;

                foreach (var item in Ingredients)
                {
                    var compare = MenuItem?.Ingredients?
                        .AsQueryable()
                        .FirstOrDefault(i => i.Id == item.Id);

                    if (compare == null)
                        added.Add(item);
                }

                return added;
            }
        }

        /// <summary>
        /// All ingredients removed from the base item
        /// </summary>
        [NotMapped]
        public List<Ingredient> RemovedIngredients
        {
            get
            {
                var removed = new List<Ingredient>();

                if (MenuItem?.Ingredients == null)
                    return removed;

                foreach (var item in MenuItem.Ingredients)
                {
                    var compare = Ingredients?
                        .AsQueryable()
                        .FirstOrDefault(i => i.Id == item.Id);

                    if (compare == null)
                        removed.Add(item);
                }

                return removed;
            }
        }

        /// <summary>
        /// Retrieves the total price of the menu line item
        /// </summary>
        [NotMapped]
        public float LinePrice
        {
            get
            {
                MenuItem.Varient = MenuItemVarient;
                float price = MenuItem.Price;

                foreach (var item in AddedIngredients)
                {
                    price += item.Price;
                }

                return price;
            }
        }

        public override string ToString()
        {
            var str = string.Empty;

            if (MenuItem != null)
                MenuItem.Varient = MenuItemVarient;

            str += $"{MenuItem?.MenuItemVarients?[MenuItem.Varient].Descriptor} {MenuItem?.Name}";

            if (AddedIngredients.Any() || RemovedIngredients.Any())
            {
                str += " --";
                foreach (var ingredient in AddedIngredients)
                {
                    str += $" Add: {ingredient.Name},";
                }

                foreach (var ingredient in RemovedIngredients)
                {
                    str += $" No {ingredient.Name},";
                }

                str = str.Remove(str.Length - 1);
            }

            str += $" -- {LinePrice.ToString("C")}";

            return str;
        }
    }
}
