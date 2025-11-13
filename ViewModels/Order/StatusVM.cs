using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.ViewModels.Order
{
    public class StatusVM
    {
        public Data.DataModels.Order Order { get; set; }

        public OrderStage Stage { get => Order.Stage; }

        public DateTime OrderPlaced { get => Order.OrderPlaced; }

        public Customer? Customer { get => Order.Customer; }

        public string GetClassesForListItem(OrderStage stage)
        {
            var classes = "";
            switch (stage)
            {
                case OrderStage.Recieved:
                    if (Stage == OrderStage.Recieved)
                        classes = "list-group-item-info border border-dark border-5";
                    else
                        classes = "list-group-item-success";
                    return classes;

                case OrderStage.InProgress:
                    if (Stage == OrderStage.InProgress)
                        classes = "list-group-item-info border border-dark border-5";
                    else if (Stage < OrderStage.InProgress)
                        classes = "list-group-item-light";
                    else
                        classes = "list-group-item-success";
                    return classes;

                case OrderStage.Ready:
                    if (Stage == OrderStage.Ready)
                        classes = "list-group-item-info border border-dark border-5";
                    else if (Stage < OrderStage.Ready)
                        classes = "list-group-item-light";
                    else
                        classes = "list-group-item-success";
                    return classes;

                default:
                case OrderStage.Complete:
                    if (Stage == OrderStage.Complete)
                        classes = "list-group-item-info border border-dark border-5";
                    else if (Stage < OrderStage.Complete)
                        classes = "list-group-item-light";
                    else
                        classes = "list-group-item-success";
                    return classes;
            }
        }

        public string GetClassesForListImg(OrderStage stage)
        {
            var classes = "";
            switch (stage)
            {
                case OrderStage.Recieved:
                    return classes;

                case OrderStage.InProgress:
                    if (Stage < OrderStage.InProgress)
                        classes = "desaturate";
                    return classes;

                case OrderStage.Ready:
                    if (Stage < OrderStage.Ready)
                        classes = "desaturate";
                    return classes;

                default:
                case OrderStage.Complete:
                    if (Stage < OrderStage.Complete)
                        classes = "desaturate";
                    return classes;
            }
        }
    }
}
