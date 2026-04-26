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

        public OrderStageLegacy StageLegacy { get; set; } = OrderStageLegacy.Recieved;

        public string ListItemClasses
        {
            get
            {
                var classes = string.Empty;

                switch (StageLegacy)
                {
                    default:
                    case OrderStageLegacy.Recieved:
                        classes += "order-terminal-item-received";
                        break;
                    case OrderStageLegacy.InProgress:
                        classes += "order-terminal-item-in-progress";
                        break;
                    case OrderStageLegacy.Ready:
                        classes += "order-terminal-item-ready";
                        break;
                    case OrderStageLegacy.Complete:
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
