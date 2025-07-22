using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.Users
{
    public class NewUserVM
    {
        private string _roles = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.PhoneNumber)]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Roles
        {
            get => _roles;
            set => _roles = value.ToLower();
        }
    }
}
