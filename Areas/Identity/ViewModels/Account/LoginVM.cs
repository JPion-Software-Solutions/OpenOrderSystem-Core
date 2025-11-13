using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Identity.ViewModels.Account
{
    public class LoginVM
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
