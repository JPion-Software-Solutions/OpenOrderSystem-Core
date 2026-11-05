using System.ComponentModel.DataAnnotations;

// This type's full name appears in System.Text.Json error messages returned by the legacy API, so it keeps
// its original namespace from the removed API area. See API/Legacy/Models/__OriginalTypeNames.cs.
namespace OpenOrderSystem.Core.Areas.API.Legacy.Models
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
