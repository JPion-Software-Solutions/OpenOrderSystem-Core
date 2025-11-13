using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Areas.API.Models;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;

namespace OpenOrderSystem.Core.Areas.API.DTO
{
    public class OrderResponse
    {
        public int Id { get; set; }
        public string? DiscountId { get; set; }
        public DateTime OrderPlaced { get; set; }
        public DateTime? OrderInprogress { get; set; }
        public DateTime? OrderReady { get; set; }
        public DateTime? OrderComplete { get; set; }
        public Customer? Customer { get; set; }
        public float LineItemTotal { get; set; }
        public float Subtotal { get; set; }
        public float Tax { get; set; }
        public float Total { get; set; }
        public double MinutesToReady { get; set; }
        public OrderStage Stage { get; set; }
        public float? DiscountAmount { get; set; }
        public List<OrderLine> LineItems { get; set; } = new();

        public Order ToOrder()
        {
            return new Order
            {
                Id = Id,
                OrderPlaced = OrderPlaced,
                OrderInprogress = OrderInprogress,
                OrderReady = OrderReady,
                OrderComplete = OrderComplete,
                Customer = Customer,
                LineItems = LineItems,
                Discount = new PrecalculatedDiscount(DiscountAmount ?? 0.00f),
                DiscountId = DiscountId
            };
        }
    }
}
