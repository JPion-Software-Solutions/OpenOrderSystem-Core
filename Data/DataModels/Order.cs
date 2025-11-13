using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using OpenOrderSystem.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace OpenOrderSystem.Core.Data.DataModels
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

        [NotMapped]
        public float LineItemTotal
        {
            get
            {
                float subtotal = 0;

                foreach (var line in LineItems)
                {
                    subtotal += line.LinePrice;
                }

                return subtotal;
            }
        }

        /// <summary>
        /// Calculates the order's subtotal
        /// </summary>
        [NotMapped]
        public float Subtotal
        {
            get
            {
                float subtotal = LineItemTotal;

                if (Discount != null)
                {
                    subtotal -= Discount.GetDiscount(this);
                }

                return subtotal;
            }
        }

        /// <summary>
        /// Calculate the order tax
        /// </summary>
        [NotMapped]
        public float Tax { get => Subtotal * 0.06f; }

        /// <summary>
        /// Calculate the order total.
        /// </summary>
        [NotMapped]
        public float Total { get => Subtotal + Tax; }

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
