using System.Text.Json.Serialization;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.API.Legacy.Models;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;

namespace OpenOrderSystem.Core.API.Legacy.DTO
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
        // Serialized as "stage": the legacy API's shape is locked and clients (e.g. SOTClient.js) read order.stage.
        [JsonPropertyName("stage")]
        public OrderStageLegacy StageLegacy { get; set; }
        public float? DiscountAmount { get; set; }
        public List<OrderLineLegacy> LineItems { get; set; } = new();

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
