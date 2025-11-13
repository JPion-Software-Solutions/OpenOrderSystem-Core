using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal
{
    public class OrderListVM
    {
        public OrderListVM()
        {
            EnabledButtons = new Dictionary<OrderTerminalButtons, bool>
            {
                { OrderTerminalButtons.Info, false },
                { OrderTerminalButtons.Next, false },
                { OrderTerminalButtons.Cancel, false },
                { OrderTerminalButtons.Timer, false },
                { OrderTerminalButtons.Done, false },
                { OrderTerminalButtons.Print, false },
                { OrderTerminalButtons.Edit, false }
            };
        }


        /// <summary>
        /// Subset of orders
        /// </summary>
        public List<Order> Orders { get; set; } = new List<Order>();

        /// <summary>
        /// Message displayed if orders list contains no orders
        /// </summary>
        public string NullMessage { get; set; } = string.Empty;

        public bool ShowAllInfo { get; set; } = false;

        /// <summary>
        /// True if there are any orders
        /// </summary>
        public bool HasOrders { get => Orders.Any(); }

        /// <summary>
        /// 
        /// </summary>
        public Dictionary<OrderTerminalButtons, bool> EnabledButtons { get; private set; }

        public OrderStage Stage { get; set; } = OrderStage.Recieved;

        public string ListItemClasses
        {
            get
            {
                var classes = string.Empty;

                switch (Stage)
                {
                    default:
                    case OrderStage.Recieved:
                        classes += "order-terminal-item-received";
                        break;
                    case OrderStage.InProgress:
                        classes += "order-terminal-item-in-progress";
                        break;
                    case OrderStage.Ready:
                        classes += "order-terminal-item-ready";
                        break;
                    case OrderStage.Complete:
                        classes += "order-terminal-item-complete";
                        break;

                }

                return classes;
            }
        }
    }

    public enum OrderTerminalButtons
    {
        Info,
        Next,
        Cancel,
        Timer,
        Done,
        Print,
        Edit
    }
}
