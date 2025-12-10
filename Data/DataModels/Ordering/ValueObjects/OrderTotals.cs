namespace OpenOrderSystem.Core.Data.DataModels.Ordering.ValueObjects
{
    public class OrderTotals
    {
        public float GrossSubtotal { get; set; }

        public float NetSubtotal { get; set; }

        public float Discount { get; set; }

        public float Tax { get; set; }

        public float Total { get; set; }

        public Dictionary<string, float> AdditionalAdjustments { get; set; } = new Dictionary<string, float>();
    }
}
