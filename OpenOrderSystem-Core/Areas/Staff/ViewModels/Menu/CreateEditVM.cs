using OpenOrderSystem.Core.Areas.Staff.Models;
using OpenOrderSystem.Core.Data.DataModels;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.Menu
{
    public class CreateEditVM
    {
        public enum Actions
        {
            Create,
            Edit
        }

        public Actions Action { get; set; } = Actions.Create;

        public int? Id { get; set; } = null;

        public Dictionary<int, List<Ingredient>> AvailableIngredients { get; set; } = new Dictionary<int, List<Ingredient>>();

        public List<ProductCategory> Categories { get; set; } = new List<ProductCategory>();

        public List<ImageModel> Images { get; set; } = new List<ImageModel>();

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Varients { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a product image")]
        public string ImageUrl { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        public int CategoryId { get; set; }

        public int Priority { get; set; }
    }
}
