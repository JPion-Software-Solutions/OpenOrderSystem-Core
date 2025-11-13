namespace OpenOrderSystem.Core.Data.DataModels.DiscountCodes
{
    public class PercentDiscountCode : BaseDiscountCode
    {
        private float _discount;
        private string _error = string.Empty;

        public float DiscountPercent
        {
            get => _discount * 100;
            set => _discount = value > 0 ? value <= 100 ? value / 100 : 1 : 0;

        }

        public override string ErrorReason => _error;

        public override float GetDiscount(Order order, bool forceValid = false)
        {
            float discount = 0;

            if (ValidateCoupon(order) || forceValid)
            {
                discount = order.LineItemTotal * _discount;
            }

            return discount;
        }

        public override bool ValidateCoupon(Order order)
        {
            var validMenu = new List<string>();

            foreach (var item in WhiteListItemsVarients ?? new List<MenuItemVarient>())
            {
                validMenu.Add($"{item.Descriptor} {item.MenuItem?.Name}");
            }

            var validMenuStr = validMenu.Any() ? string.Join(", ", validMenu) : "Any Menu Item";

            if (IsArchived) return false;

            if (WhiteListItemsVarients != null)
            {
                var validItems = FilterValidItems(order);

                if (!validItems.Any())
                {
                    _error = $"Invalid Discount. Add at least 1 of the following items to your " +
                        $"order to recieve your discount. Valid On: {validMenuStr}";
                }

                return MetMinimumSpend(order.LineItemTotal) && (validItems?.Any() ?? false);
            }

            return MetMinimumSpend(order.LineItemTotal);
        }
    }
}
