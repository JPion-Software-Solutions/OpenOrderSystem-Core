using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.API.Models
{
    public class CartUpdateItemModel
    {
        [Required]
        [RegularExpression(@"^[{]?[0-9a-fA-F]{8}[-]?([0-9a-fA-F]{4}[-]?){3}[0-9a-fA-F]{12}[}]?$",
            ErrorMessage = "Invalid cart Id, cart Id's should be in standard GUID format.")]
        public string CartId { get; set; } = string.Empty;

        [MaxLength(128, ErrorMessage = "Please limit line comments to 128 characters or less.")]
        public string? LineComments { get; set; } = null;

        public int? VarientIndex { get; set; } = null;

        [Required]
        public int Index { get; set; }

        [Required]
        public int[] IngredientIds { get; set; } = new int[0];
    }
}
