using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.ViewModels.Order
{
    public class StatusVM
    {
        public Data.DataModels.Order Order { get; set; }

        public OrderStageLegacy StageLegacy { get => Order.StageLegacy; }

        public DateTime OrderPlaced { get => Order.OrderPlaced; }

        public Customer? Customer { get => Order.Customer; }

        public string GetClassesForListItem(OrderStageLegacy stageLegacy)
        {
            var classes = "";
            switch (stageLegacy)
            {
                case OrderStageLegacy.Recieved:
                    if (StageLegacy == OrderStageLegacy.Recieved)
                        classes = "list-group-item-info border border-dark border-5";
                    else
                        classes = "list-group-item-success";
                    return classes;

                case OrderStageLegacy.InProgress:
                    if (StageLegacy == OrderStageLegacy.InProgress)
                        classes = "list-group-item-info border border-dark border-5";
                    else if (StageLegacy < OrderStageLegacy.InProgress)
                        classes = "list-group-item-light";
                    else
                        classes = "list-group-item-success";
                    return classes;

                case OrderStageLegacy.Ready:
                    if (StageLegacy == OrderStageLegacy.Ready)
                        classes = "list-group-item-info border border-dark border-5";
                    else if (StageLegacy < OrderStageLegacy.Ready)
                        classes = "list-group-item-light";
                    else
                        classes = "list-group-item-success";
                    return classes;

                default:
                case OrderStageLegacy.Complete:
                    if (StageLegacy == OrderStageLegacy.Complete)
                        classes = "list-group-item-info border border-dark border-5";
                    else if (StageLegacy < OrderStageLegacy.Complete)
                        classes = "list-group-item-light";
                    else
                        classes = "list-group-item-success";
                    return classes;
            }
        }

        public string GetClassesForListImg(OrderStageLegacy stageLegacy)
        {
            var classes = "";
            switch (stageLegacy)
            {
                case OrderStageLegacy.Recieved:
                    return classes;

                case OrderStageLegacy.InProgress:
                    if (StageLegacy < OrderStageLegacy.InProgress)
                        classes = "desaturate";
                    return classes;

                case OrderStageLegacy.Ready:
                    if (StageLegacy < OrderStageLegacy.Ready)
                        classes = "desaturate";
                    return classes;

                default:
                case OrderStageLegacy.Complete:
                    if (StageLegacy < OrderStageLegacy.Complete)
                        classes = "desaturate";
                    return classes;
            }
        }
    }
}
