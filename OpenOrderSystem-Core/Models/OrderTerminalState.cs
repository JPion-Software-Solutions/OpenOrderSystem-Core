using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.Models
{
    public class OrderTerminalState
    {
        public static List<OrderStage> CheckForUpdates(List<Order> activeOrders, OrderTerminalState lastKnownState)
        {
            List<OrderStage> stagesToRefresh = new List<OrderStage>();

            foreach (var order in activeOrders)
            {
                //All stages need to be refreshed.
                if (stagesToRefresh.Count >= Enum.GetNames(typeof(OrderStage)).Length)
                {
                    break;
                }

                //a new order has come in
                else if (!lastKnownState.OrderStatus.ContainsKey(order.Id))
                {
                    if (!stagesToRefresh.Contains(OrderStage.Recieved))
                        stagesToRefresh.Add(OrderStage.Recieved);
                }

                //an order's stage has changed since the last update
                else if (lastKnownState.OrderStatus[order.Id] != order.Stage)
                {
                    if (!stagesToRefresh.Contains(order.Stage))
                        stagesToRefresh.Add(order.Stage);

                    if (!stagesToRefresh.Contains(lastKnownState.OrderStatus[order.Id]))
                        stagesToRefresh.Add(lastKnownState.OrderStatus[order.Id]);
                }

            }

            return stagesToRefresh;
        }

        /// <summary>
        /// Dictionary containing order numbers as keys and the last known stage for that order
        /// </summary>
        public Dictionary<int, OrderStage> OrderStatus { get; set; } = new Dictionary<int, OrderStage>();
    }
}
