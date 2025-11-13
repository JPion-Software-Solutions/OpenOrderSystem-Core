using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Configuration.ViewModels.InitialSetup
{
    public class ConfirmEmailVM
    {
        [Required]
        [Display(Name = "Confirmation Code")]
        public string ConfirmationCode { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }
}
