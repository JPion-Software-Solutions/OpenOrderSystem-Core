using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.ViewModels.Shared;
using System.Text.Json;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.Categories.Product
{
    public class CreateEditVM
    {
        private ProductCategory _category;

        public CreateEditVM()
        {
            _category = new ProductCategory();
        }
        public CreateEditVM(List<Ingredient> ingredients, List<MenuItem> menuItems, ProductCategory? category = null)
        {
            _category = category ?? new ProductCategory();
            Action = category == null ? CrudAction.Create : CrudAction.Edit;
            Ingredients = ingredients;
            MenuItems = menuItems;

            //prefill ingredient ids field
            var ids = new List<int>();
            foreach (var ingredient in ingredients)
            {
                if (ingredient.ProductCategories?.Contains(_category) ?? false)
                    ids.Add(ingredient.Id);
            }
            IngredientIds = JsonSerializer.Serialize(ids);

            //populate menuitemIds field
            ids.Clear();
            foreach (var menuItem in menuItems)
            {
                if (menuItem.ProductCategoryId == Id)
                    ids.Add(menuItem.Id);
            }
            MenuItemIds = JsonSerializer.Serialize(ids);
        }

        public ProductCategory Category { get => _category; }

        public CrudAction Action { get; set; } = CrudAction.Create;

        public string IngredientIds { get; set; } = string.Empty;
        public string MenuItemIds { get; set; } = string.Empty;

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

        public string Description
        {
            get => _category.Description;
            set => _category.Description = value;
        }

        public int Priority
        {
            get => _category.Priority;
            set => _category.Priority = value;
        }

        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

        public List<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

        public int[] GetLinkedIngredients() => JsonSerializer.Deserialize<int[]>(IngredientIds) ?? Array.Empty<int>();
        public int[] GetLinkedMenuItems() => JsonSerializer.Deserialize<int[]>(MenuItemIds) ?? Array.Empty<int>();
    }
}
