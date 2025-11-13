namespace OpenOrderSystem.Core.Data.DataModels.DiscountCodes
{
    public class FixedAmountDiscountCode : BaseDiscountCode
    {
        private string _error = string.Empty;
        public override string ErrorReason => _error;

        public float Discount { get; set; }

        public override float GetDiscount(Order order, bool forceValid = false)
        {
            if (!(ValidateCoupon(order) || forceValid))
            {
                return 0f;
            }

            return Discount;
        }

        public override bool ValidateCoupon(Order order)
        {
            //validates code is still active
            bool isActive = !IsArchived;

            //validates the order total meets the minimum purchase requirement
            bool metMinimum = MetMinimumSpend(order.LineItemTotal);

            //sets the error message so customer's can recieve feedback if the coupon isn't working.
            if (!isActive) _error = "Invalid discount code.";
            else if (!metMinimum) _error = $"Invalid discount. You must spend at least " +
                    $"{MinimumSpend?.ToString("C")} to receive the discount.";

            return isActive && metMinimum;
        }
    }
}
