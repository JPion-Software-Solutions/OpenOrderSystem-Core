using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.API.Models
{
    public class CartCustomer
    {
        [Required]
        [RegularExpression(@"^[{]?[0-9a-fA-F]{8}[-]?([0-9a-fA-F]{4}[-]?){3}[0-9a-fA-F]{12}[}]?$",
            ErrorMessage = "Invalid cart Id, cart Id's should be in standard GUID format.")]
        public string CartId { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Phone]
        [Required]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        [Required]
        public string Email { get; set; } = string.Empty;

        public bool SmsUpdates { get; set; } = true;
        public bool EmailUpdates { get; set; } = false;
    }
}
