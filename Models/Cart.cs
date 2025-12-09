using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using OpenOrderSystem.Core.Data.DataModels.Ordering.Entities;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Models
{
    public class Cart
    {
        private string? _promoCode = null;
        public Cart()
        {
            Id = Guid.NewGuid().ToString();
            CartLastActive = DateTime.UtcNow;
            Order = new Order();
        }

        /// <summary>
        /// Unique GUID used to identify this cart
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Tracks when the cart was opened so abandoned carts can be purged.
        /// </summary>
        public DateTime CartLastActive { get; set; }

        /// <summary>
        /// Order associated with cart
        /// </summary>
        public Order Order { get; set; }

        [MaxLength(20)]
        public string? PromoCode { get => _promoCode; set => _promoCode = value?.ToUpper(); }

        public BaseDiscountCode? Promo { get; set; }

        public float Discount => Promo == null ? 0 : Promo.GetDiscount(Order);

        public Customer? Customer
        {
            get => Order.Customer;
            set => Order.Customer = value;
        }

        public List<OrderLine> LineItems
        {
            get => Order.LineItems;
            set => Order.LineItems = value;
        }

        public bool IsExistingOrder { get; set; } = false;

        /// <summary>
        /// Checks for expired carts, returns true if cart hasn't been used in over 60 minutes.
        /// </summary>
        public bool Expired { get => DateTime.UtcNow > CartLastActive.AddMinutes(60); }
    }
}
