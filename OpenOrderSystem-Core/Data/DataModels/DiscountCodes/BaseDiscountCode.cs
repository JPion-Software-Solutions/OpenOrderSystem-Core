using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenOrderSystem.Core.Data.DataModels.DiscountCodes
{
    [PrimaryKey(nameof(Code))]
    public abstract class BaseDiscountCode
    {
        private string _code = string.Empty;
        private bool _isArchived = false;

        [Required]
        [Column(name: "Id")]
        [MaxLength(20)]
        public string Code { get => _code; set => _code = value.ToUpper(); }

        /// <summary>
        /// Very brief summary of what the coupon does (ex: 10% off)
        /// </summary>
        [Required]
        [MaxLength(40)]
        public string Name { get; set; } = string.Empty;


        /// <summary>
        /// Tracks the number of times the discount has been redeemed
        /// </summary>
        public int Redemptions { get => Orders?.Count ?? 0; set { } }

        /// <summary>
        /// Sets an upper limit on how many times a discount code may be used, 
        /// may be set to null if no limit is desired
        /// </summary>
        public int? MaxRedemptions { get; set; }

        /// <summary>
        /// Minimum amount a customer needs to spend to activate the coupon
        /// </summary>
        public float? MinimumSpend { get; set; }

        /// <summary>
        /// When the discount code was created.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Date/Time the code will expire. If set to null the code is non-expiring and 
        /// will need to be manually canceled. 
        /// </summary>
        public DateTime? Expiration { get; set; }

        /// <summary>
        /// Allows you to "archive" a code rendering it unusable even if it would otherwise 
        /// be invalid
        /// </summary>
        public bool IsArchived
        {
            get =>
                DateTime.Now > (Expiration ?? DateTime.Now.AddMinutes(1)) ||
                Redemptions >= (MaxRedemptions ?? Redemptions + 1) ||
                _isArchived;
            set => _isArchived = value;
        }

        /// <summary>
        /// Item varients that do not count for the coupon. 
        /// </summary>
        public List<MenuItemVarient>? WhiteListItemsVarients { get; set; }

        /// <summary>
        /// Orders redeeming the coupon code.
        /// </summary>
        public List<Order>? Orders { get; set; }

        [NotMapped]
        public abstract string ErrorReason { get; }

        /// <summary>
        /// Verify an order meets the minimum requirements to redeem the given coupon
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public abstract bool ValidateCoupon(Order order);

        public abstract float GetDiscount(Order order, bool forceValid = false);

        public virtual List<MenuItemVarient> FilterValidItems(Order order)
        {
            var validItemsInOrder = new List<MenuItemVarient>();

            if (WhiteListItemsVarients?.Count < 1)
            {
                foreach (var line in order.LineItems)
                {
                    validItemsInOrder.Add(line.MenuItem?.MenuItemVarients?[line.MenuItemVarient] ??
                        throw new NullReferenceException());
                }
            }
            else
            {
                foreach (var line in order.LineItems)
                {
                    var varient = line.MenuItem?.MenuItemVarients?[line.MenuItemVarient] ??
                        throw new NullReferenceException();

                    if (WhiteListItemsVarients.FirstOrDefault(w => varient.Id == w.Id) != null)
                        validItemsInOrder.Add(varient);
                }
            }

            return validItemsInOrder;
        }

        public virtual bool MetMinimumSpend(float ordersubtoatl)
        {
            if (MinimumSpend == null) return true;
            else return ordersubtoatl >= MinimumSpend;
        }
    }
}
