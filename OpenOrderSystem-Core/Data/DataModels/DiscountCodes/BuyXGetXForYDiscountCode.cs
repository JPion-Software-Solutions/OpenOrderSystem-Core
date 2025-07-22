namespace OpenOrderSystem.Core.Data.DataModels.DiscountCodes
{
    public class BuyXGetXForYDiscountCode : BaseDiscountCode
    {
        private string _error = "";
        private float _discount;

        public override string ErrorReason => _error;

        public int BuyQty { get; set; }

        public int GetQty { get; set; }

        public float DiscountPercent
        {
            get => _discount * 100;
            set => _discount = value > 0 ? value <= 100 ? value / 100 : 1 : 0;

        }

        public override float GetDiscount(Order order, bool forceValid = false)
        {
            var validItems = FilterValidItems(order);

            float discountTotal = 0;

            //validate coupon
            bool isValid = ValidateCoupon(order) || forceValid;
            if (!isValid) return 0f;

            //find least valuable item(s)
            var orderLinesByPrice = order.LineItems
                .AsQueryable()
                .Where(l => validItems.Contains(l.MenuItem.MenuItemVarients[l.MenuItemVarient]))
                .OrderBy(li => li.LinePrice);

            var discountableQty = orderLinesByPrice.Count() - BuyQty;

            if (discountableQty > GetQty)
                discountableQty = GetQty;

            var discountedItems = orderLinesByPrice
                .Take(discountableQty)
                .ToList();

            //calculate discount against least valuable item(s)
            foreach (var discountedItem in discountedItems)
            {
                discountTotal += discountedItem.LinePrice * _discount;
            }

            return discountTotal;
        }

        public override bool ValidateCoupon(Order order)
        {
            var validMenu = new List<string>();

            foreach (var item in WhiteListItemsVarients ?? new List<MenuItemVarient>())
            {
                validMenu.Add($"{item.Descriptor} {item.MenuItem?.Name}");
            }

            var validMenuStr = validMenu.Any() ? string.Join(", ", validMenu) : "Any Menu Item";

            var validItems = FilterValidItems(order);

            bool isActive = !IsArchived;

            bool conditionMet = validItems.Count >= BuyQty + 1;

            _error = isActive ? conditionMet ? "" :
                //error for a valid code but incorrect cart items
                $"Invalid Discount. Add at least {BuyQty + 1} of the following items to your order to recieve discount. Valid On: {validMenuStr}" :

                //error for an invalid code
                "Invalid Discount Code.";

            return conditionMet && isActive;
        }
    }
}
