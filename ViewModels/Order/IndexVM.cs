using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.ViewModels.Order
{
    public class IndexVM
    {
        [Display(Name = "Order Number")]

        public int? OrderId { get; set; }

        public string? Name { get; set; }

        public string? Phone { get; set; }

        public List<Data.DataModels.Order> MyOrders { get; set; } = new List<Data.DataModels.Order>();
    }
}
