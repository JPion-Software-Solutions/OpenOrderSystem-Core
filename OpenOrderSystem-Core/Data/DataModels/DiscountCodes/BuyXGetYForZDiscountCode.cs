namespace OpenOrderSystem.Core.Data.DataModels.DiscountCodes
{
    public class BuyXGetYForZDiscountCode : BaseDiscountCode
    {
        private string _error = string.Empty;
        public override string ErrorReason => _error;

        public List<DiscountCodeItem>? Items { get; set; }

        public override float GetDiscount(Order order, bool forceValid = false)
        {
            throw new NotImplementedException();
        }

        public override bool ValidateCoupon(Order order)
        {
            throw new NotImplementedException();
        }
    }
}
