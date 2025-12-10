using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using OpenOrderSystem.Core.Data.DataModels.Ordering.ValueObjects;
using OpenOrderSystem.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenOrderSystem.Core.Data.DataModels.Ordering.Entities
{
    public class Order
    {
        /// <summary>
        /// Order Id Number
        /// </summary>
        public int Id { get; set; }

        public DateTime OrderPlaced { get; set; }
        public DateTime? OrderInprogress { get; set; }
        public DateTime? OrderReady { get; set; }
        public DateTime? OrderComplete { get; set; }

        /// <summary>
        /// Time between order marked as in-progress and ready 
        /// </summary>
        public double MinutesToReady { get; private set; } = 15;

        /// <summary>
        /// Id of the customer object containing the customer 
        /// info. (Purged after 24 hours)
        /// </summary>
        public int? CustomerId { get; set; }

        /// <summary>
        /// Customer information
        /// </summary>
        public Customer? Customer { get; set; }

        public List<PriceAdjustment> PriceAdjustments { get; set; } = new List<PriceAdjustment>();

        public OrderTotals Totals { get; set; } = new OrderTotals();

        /// <summary>
        /// JSON data containing the details of a completed order using the LockedOrder model.
        /// </summary>
        public string Locked { get; set; } = string.Empty;

        [NotMapped]
        public LockedOrder? LockedOrderDetail
        {
            get
            {
                if (!string.IsNullOrEmpty(Locked))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<LockedOrder>(Locked);
                    }
                    catch (JsonException ex)
                    {
                        return null;
                    }
                }

                return null;
            }
            set
            {
                Locked = JsonSerializer.Serialize(value);
            }
        }

        public List<OrderLine> LineItems { get; set; } = new List<OrderLine>();

        [MaxLength(128)]
        public string? OrderComments { get; set; }

        public string? DiscountId { get; set; }
        public BaseDiscountCode? Discount { get; set; }

        /// <summary>
        /// DEPRECIATED: WILL BE REMOVED IN FUTURE UPDATE. PLEASU USE NEW TOTALS STRUCTURE AND CalculateOrderTotal METHOD! Calculates the order's total before any adjustments
        /// </summary>
        [NotMapped]
        public float LineItemTotal => Totals.GrossSubtotal;

        /// <summary>
        /// DEPRECIATED: WILL BE REMOVED IN FUTURE UPDATE. PLEASU USE NEW TOTALS STRUCTURE AND CalculateOrderTotal METHOD! Calculates the order's subtotal
        /// </summary>
        [NotMapped]
        public float Subtotal => Totals.NetSubtotal;

        /// <summary>
        /// DEPRECIATED: WILL BE REMOVED IN FUTURE UPDATE. PLEASU USE NEW TOTALS STRUCTURE AND CalculateOrderTotal METHOD! Calculate the order tax
        /// </summary>
        [NotMapped]
        public float Tax => Totals.Tax;

        /// <summary>
        /// DEPRECIATED: WILL BE REMOVED IN FUTURE UPDATE. PLEASU USE NEW TOTALS STRUCTURE AND CalculateOrderTotal METHOD! Calculate the order total.
        /// </summary>
        [NotMapped]
        public float Total => Totals.Total;

        [NotMapped]
        public OrderStage Stage
        {
            get
            {
                if (OrderComplete != null)
                    return OrderStage.Complete;

                if (OrderReady != null)
                    return OrderStage.Ready;

                if (OrderInprogress != null)
                    return OrderStage.InProgress;

                return OrderStage.Recieved;
            }
        }

        public void CompleteStage()
        {
            if (Stage == OrderStage.Ready)
            {
                LockedOrderDetail = LockedOrder.Create(this);
                OrderComplete = DateTime.UtcNow;
            }

            else if (Stage == OrderStage.InProgress)
                OrderReady = DateTime.UtcNow;

            else if (Stage == OrderStage.Recieved)
                OrderInprogress = DateTime.UtcNow;
        }

        /// <summary>
        /// Adds an arbitray amount of time to the MinutesToReady
        /// </summary>
        /// <param name="time">minutes to add</param>
        public void AddToTimer(double time) => MinutesToReady += time;

        public TimerStatus CheckTimer()
        {
            if (OrderInprogress == null)
                return TimerStatus.NotApplicable;

            else if (DateTime.UtcNow > OrderInprogress.Value.AddMinutes(MinutesToReady))
                return TimerStatus.TimeUp;

            else if (DateTime.UtcNow > OrderInprogress.Value.AddMinutes(MinutesToReady - 2))
                return TimerStatus.LessThanTwo;

            else
                return TimerStatus.TimeGood;
        }

        public OrderTotals CalculateOrderTotals(TotalCalculationContext context)
        {
            Totals = new OrderTotals(); //reset to 0 for clean count.

            //Calculate the GrossSubtotal
            foreach (var line in LineItems) Totals.GrossSubtotal += line.LinePrice;

            //Calculate discount
            foreach (var promo in PriceAdjustments.Where(a => a.MatchesSource("OOSCore.Promotion"))) Totals.Discount += promo.Amount;

            //Calculate additional price adjustments
            var additionalAdjustmentTotal = 0f;
            foreach (var adjustment in PriceAdjustments.Where(a => !a.MatchesSource("OOSCore.Promotion"))) 
            {
                additionalAdjustmentTotal += adjustment.Amount;
                Totals.AdditionalAdjustments[adjustment.Reason] = adjustment.Amount;
            }

            //calculate net subtotal
            Totals.NetSubtotal = Totals.GrossSubtotal + Totals.Discount + additionalAdjustmentTotal;

            //calculate tax
            Totals.Tax = MathF.Round(Totals.NetSubtotal * context.TaxRate, 2);

            //calculate final total
            Totals.Total = Totals.NetSubtotal + Totals.Tax;

            return Totals;
        }
    }

    public enum TimerStatus
    {
        TimeUp,
        LessThanTwo,
        TimeGood,
        NotApplicable
    }

    public enum OrderStage
    {
        Recieved,
        InProgress,
        Ready,
        Complete
    }
}
