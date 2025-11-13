using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class DiscountCodeItem
    {
        public int Id { get; set; }

        public string DiscountId { get; set; } = string.Empty;
        public BuyXGetYForZDiscountCode? Discount { get; set; }

        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        public string? VarientFilter { get; set; }

        public ItemRelationType ItemRelationType { get; set; }

        public bool ValidVarient(MenuItem menuItem)
        {
            if (menuItem.Id != MenuItem?.Id) return false;
            if (VarientFilter == null) return true;

            var varients = VarientFilter?
                .Replace(" ", "")
                .ToLower()
                .Split(',') ?? Array.Empty<string>();

            var itemVarientName = menuItem.MenuItemVarients?[menuItem.Varient].Descriptor.ToLower() ?? string.Empty;
            return varients.Contains(itemVarientName);
        }
    }
}
