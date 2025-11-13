using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Configuration.ViewModels.InitialSetup
{
    public class PrinterSetupVM
    {
        [Required]
        [Display(Name = "Printer Address")]
        public string PrinterAddress { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Printer Port")]
        public string PrinterPort { get; set; } = string.Empty;
    }
}
