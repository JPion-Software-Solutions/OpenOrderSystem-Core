namespace OpenOrderSystem.Core.Models
{
    public class PrinterStatus
    {
        public DateTime LastOnline { get; set; }

        public bool IsPaperLow { get; set; }

        public bool IsPaperOut { get; set; }

        public bool IsErrorState { get; set; }

        public bool IsBridgeActive => LastOnline.AddMinutes(1) > DateTime.UtcNow;

        public bool IsConnected { get; set; }

        public bool IsReady => IsBridgeActive && IsConnected;

        public bool IsCoverOpen { get; set; }

        public int JobsWaiting => Queue.Count;

        public Dictionary<string, PrintJob> Queue { get; private set; } = new Dictionary<string, PrintJob>();
    }
}
