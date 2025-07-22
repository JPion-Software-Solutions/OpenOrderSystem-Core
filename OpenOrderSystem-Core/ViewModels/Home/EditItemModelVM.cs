using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;

namespace OpenOrderSystem.Core.ViewModels.Home
{
    public class EditItemModelVM
    {
        public string CartId { get; set; } = string.Empty;
        public Cart? Cart { get; set; }
        public int Index { get; set; }
        public Dictionary<IngredientCategory, List<Ingredient>> AvailableIngredients { get; private set; } = new Dictionary<IngredientCategory, List<Ingredient>>();
        public List<Ingredient> CurrentIngredients { get; set; } = new List<Ingredient>();
    }
}
