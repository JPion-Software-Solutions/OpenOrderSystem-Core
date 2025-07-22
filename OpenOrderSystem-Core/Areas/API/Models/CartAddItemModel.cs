using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.API.Models
{
    public class CartAddItemModel
    {

        [Required]
        [RegularExpression(@"^[{]?[0-9a-fA-F]{8}[-]?([0-9a-fA-F]{4}[-]?){3}[0-9a-fA-F]{12}[}]?$",
            ErrorMessage = "Invalid cart Id, cart Id's should be in standard GUID format.")]
        public string CartId { get; set; } = string.Empty;

        [Required]
        public int ItemId { get; set; }

        public int Varient { get; set; } = 0;
    }
}
