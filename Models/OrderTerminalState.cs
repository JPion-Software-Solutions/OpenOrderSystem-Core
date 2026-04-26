using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.Models
{
    public class OrderTerminalState
    {
        public static List<OrderStageLegacy> CheckForUpdates(List<Order> activeOrders, OrderTerminalState lastKnownState)
        {
            List<OrderStageLegacy> stagesToRefresh = new List<OrderStageLegacy>();

            foreach (var order in activeOrders)
            {
                //All stages need to be refreshed.
                if (stagesToRefresh.Count >= Enum.GetNames(typeof(OrderStageLegacy)).Length)
                {
                    break;
                }

                //a new order has come in
                else if (!lastKnownState.OrderStatus.ContainsKey(order.Id))
                {
                    if (!stagesToRefresh.Contains(OrderStageLegacy.Recieved))
                        stagesToRefresh.Add(OrderStageLegacy.Recieved);
                }

                //an order's stage has changed since the last update
                else if (lastKnownState.OrderStatus[order.Id] != order.StageLegacy)
                {
                    if (!stagesToRefresh.Contains(order.StageLegacy))
                        stagesToRefresh.Add(order.StageLegacy);

                    if (!stagesToRefresh.Contains(lastKnownState.OrderStatus[order.Id]))
                        stagesToRefresh.Add(lastKnownState.OrderStatus[order.Id]);
                }

            }

            return stagesToRefresh;
        }

        /// <summary>
        /// Dictionary containing order numbers as keys and the last known stage for that order
        /// </summary>
        public Dictionary<int, OrderStageLegacy> OrderStatus { get; set; } = new Dictionary<int, OrderStageLegacy>();
    }
}
