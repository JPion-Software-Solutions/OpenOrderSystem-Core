using OpenOrderSystem.Core.Areas.Staff.Models;

namespace OpenOrderSystem.Core.Models
{
    public class EndOfDayReport
    {
        public DateTime Date { get; set; }

        public int OrderCount { get; set; }

        public float GrossSales { get; set; }

        public Dictionary<string, SalesData> SalesReport { get; set; } = new Dictionary<string, SalesData>();

        public SalesData PromoReport { get; set; } = new SalesData();
    }
}
