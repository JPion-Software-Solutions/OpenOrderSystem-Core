using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.API.Models
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
