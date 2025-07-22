using OpenOrderSystem.Core.Data.DataModels;
using System.Text.Json.Serialization;
using Twilio.Rest.Trunking.V1;

namespace OpenOrderSystem.Core.Models
{
    public class LockedOrder
    {
        public static LockedOrder Create(Order order)
        {
            var finalizedOrder = new LockedOrder();

            finalizedOrder.OrderNumber = $"#{order.Id}";
            finalizedOrder.PromoCode = order.DiscountId ?? "";
            finalizedOrder.Discount = order.Discount?.GetDiscount(order, true) ?? 0.0f;

            for (int i = 0; i < order.LineItems.Count; i++)
            {
                OrderLine? line = order.LineItems[i];
                finalizedOrder.LineItems[$"{line.MenuItem?.MenuItemVarients[line.MenuItemVarient].Descriptor} - {line.MenuItem?.Name}"] = line.LinePrice;
            }

            return finalizedOrder;
        }

        public string OrderNumber { get; set; } = string.Empty;

        public Dictionary<string, float> LineItems { get; set; } = new Dictionary<string, float>();

        public string PromoCode { get; set; } = string.Empty;

        public float Discount { get; set; }

        [JsonIgnore]
        public float LineTotal
        {
            get
            {
                var total = 0.0f;

                foreach (var item in LineItems.Values) total += item;

                return total;
            }
        }

        [JsonIgnore]
        public float Subtotal => LineTotal - Discount;

        [JsonIgnore]
        public float Tax => Subtotal * 0.06f;

        [JsonIgnore]
        public float Total => Subtotal + Tax;
    }
}
