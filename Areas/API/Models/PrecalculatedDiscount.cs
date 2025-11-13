using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;

namespace OpenOrderSystem.Core.Areas.API.Models
{
    public class PrecalculatedDiscount : BaseDiscountCode
    {
        private string _errorReason = string.Empty;
        private float _discount;

        public PrecalculatedDiscount(float discount)
        {
            _discount = discount;
        }

        public override string ErrorReason => _errorReason;

        public override float GetDiscount(Order order, bool forceValid = false) => _discount;

        public override bool ValidateCoupon(Order order) => true;
    }
}
