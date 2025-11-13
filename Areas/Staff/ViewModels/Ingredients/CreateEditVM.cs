using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.ViewModels.Shared;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.Ingredients
{
    public class CreateEditVM
    {
        private Ingredient _ingredient;

        public CreateEditVM()
        {
            _ingredient = new Ingredient();
        }

        public CreateEditVM(List<IngredientCategory> ingredientCategories,
            Ingredient? ingredient = null)
        {
            _ingredient = ingredient ?? new Ingredient();
            IngredientCategories = ingredientCategories;
        }

        public Ingredient Ingredient { get => _ingredient; }

        public int Id
        {
            get => _ingredient.Id;
            set => _ingredient.Id = value;
        }

        public string Name
        {
            get => _ingredient.Name;
            set => _ingredient.Name = value;
        }

        public float Price
        {
            get => _ingredient.Price;
            set => _ingredient.Price = value;
        }

        [Display(Name = "Ingredient Category")]
        public int IngredientCategoryId
        {
            get => _ingredient.CategoryId;
            set => _ingredient.CategoryId = value;
        }

        public int[] ProductCategoryIds { get; set; } = Array.Empty<int>();

        public CrudAction Action { get; set; } = CrudAction.Create;

        public List<IngredientCategory> IngredientCategories { get; set; } = new List<IngredientCategory>();
    }

}
