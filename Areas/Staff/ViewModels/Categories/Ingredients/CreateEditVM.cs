using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.ViewModels.Shared;
using System.Text.Json;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.Categories.Ingredients
{
    public class CreateEditVM
    {
        private IngredientCategory _category;

        public CreateEditVM()
        {
            _category = new IngredientCategory();
            Ingredients = new List<Ingredient>();
            IngredientIds = "";
        }

        public CreateEditVM(List<Ingredient> ingredients, IngredientCategory? ingredientCategory = null)
        {
            _category = ingredientCategory ?? new IngredientCategory();
            Action = ingredientCategory == null ? CrudAction.Create : CrudAction.Edit;
            Ingredients = ingredients;

            var ids = new List<int>();

            foreach (var ingredient in ingredients)
            {
                if (ingredient.CategoryId == Id)
                    ids.Add(ingredient.Id);
            }

            IngredientIds = JsonSerializer.Serialize(ids);
        }

        public IngredientCategory Category { get => _category; }

        public int Id
        {
            get => _category.Id;
            set => _category.Id = value;
        }

        public string Name
        {
            get => _category.Name;
            set => _category.Name = value;
        }

        public IngredientType Type
        {
            get => _category.Type;
            set => _category.Type = value;
        }

        public int Priority
        {
            get => _category.Priority;
            set => _category.Priority = value;
        }

        public string IngredientIds { get; set; }

        public List<Ingredient> Ingredients { get; set; }

        public CrudAction Action { get; set; } = CrudAction.Create;
    }
}
