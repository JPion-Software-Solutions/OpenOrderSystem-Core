using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.API.Models
{
    public class LoginModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
