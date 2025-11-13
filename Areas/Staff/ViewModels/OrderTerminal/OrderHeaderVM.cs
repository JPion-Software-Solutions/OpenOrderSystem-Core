namespace OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal
{
    public class OrderHeaderVM
    {
        public int Recieved { get; set; }
        public int InProgress { get; set; }
        public int Ready { get; set; }
        public int Complete { get; set; }
    }
}
