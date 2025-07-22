using OpenOrderSystem.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.ViewModels.Home
{
    public class CheckoutVM
    {
        private string _id;
        public Cart? Cart { get; set; }

        public string CartId
        {
            get => Cart?.Id ?? _id;
            set
            {
                if (Cart != null)
                    Cart.Id = value;

                _id = value;
            }
        }

        public string Name { get; set; } = string.Empty;

        [Phone]
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; } = string.Empty;

        [EmailAddress]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; } = string.Empty;

        public string CaptchaToken { get; set; } = string.Empty;

        [Display(Name = "Recieve order updates via text?")]
        public bool TextUpdates { get; set; } = false;
    }
}
