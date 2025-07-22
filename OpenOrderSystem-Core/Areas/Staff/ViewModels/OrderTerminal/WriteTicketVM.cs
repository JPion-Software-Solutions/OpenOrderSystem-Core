using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using OpenOrderSystem.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal
{
    public class WriteTicketVM
    {
        private BaseDiscountCode? _promo;
        private string? _promoCode;

        public List<MenuItem> Menu { get; set; } = new List<MenuItem>();
        public Cart? Cart { get; set; }
        public string CartId { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = string.Empty;

        public string? PromoCode
        {
            get => Cart?.PromoCode ?? _promoCode;
            set => _promoCode = value;
        }
        public BaseDiscountCode? Promo
        {
            get => Cart?.Promo ?? _promo;
            set => _promo = value;
        }

        public List<BaseDiscountCode> AvailablePromoCodes { get; set; } = new List<BaseDiscountCode>();

        [Required]
        [Display(Name = "Customer Phone")]
        [DataType(DataType.PhoneNumber)]
        public string CustomerPhone { get; set; } = string.Empty;
    }
}
