using PizzaPartry.tools;
using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.Areas.Staff.Models
{
    public class SalesData
    {
        public int Qty
        {
            get
            {
                int sold = 0;

                foreach (var item in VarientSalesData.Values)
                {
                    sold += item.Qty;
                }

                return sold;
            }
        }

        public float Sales
        {
            get
            {
                float sold = 0;

                foreach (var item in VarientSalesData.Values)
                {
                    sold += item.Sales;
                }

                return sold;
            }
        }

        public Dictionary<string, VarientSalesData> VarientSalesData { get; set; } = new Dictionary<string, VarientSalesData>();
    }

    public class VarientSalesData
    {
        public int Qty { get; set; }

        public float Sales { get; set; }
    }
}
