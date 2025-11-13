using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Configuration.ViewModels.InitialSetup
{
    public class CreateAdminVM
    {
        [Required(ErrorMessage = "Username is required")]
        [MaxLength(20, ErrorMessage = "Username must be between 7 and 20 characters")]
        [MinLength(7, ErrorMessage = "Username must be between 7 and 20 characters")]
        public string Username { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Required]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password")]
        [Required]
        public string ConfirmPassword { get; set; } = string.Empty;

        [DataType(DataType.EmailAddress)]
        [Required]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.PhoneNumber)]
        [Required]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
