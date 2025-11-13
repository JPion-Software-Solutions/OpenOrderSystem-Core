using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.Coupon
{
    public class IndexVM
    {
        public List<FixedAmountDiscountCode>? FixedDiscounts { get; set; }

        public List<PercentDiscountCode>? PercentDiscounts { get; set; }

        public List<BuyXGetXForYDiscountCode>? BogoDiscounts { get; set; }
    }
}
