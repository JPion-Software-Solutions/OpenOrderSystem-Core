using System.ComponentModel.DataAnnotations;

// This type's full name appears in System.Text.Json error messages returned by the legacy API, so it keeps
// its original namespace from the removed API area. See API/Legacy/Models/__OriginalTypeNames.cs.
namespace OpenOrderSystem.Core.Areas.API.Legacy.Models
{
    public class PrinterRegistrationModel
    {
        [Required]
        public string PrinterName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string Pin { get; set; } = string.Empty;

        [Required]
        public string ClientId { get; set; } = string.Empty;
    }
}
