
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Models;

namespace OpenOrderSystem.Core.Services
{
    public class PrintSpoolerService
    {
        private readonly Dictionary<string, PrinterStatus> _printerStatus = new Dictionary<string, PrinterStatus>();

        public bool IsReady { get; private set; }

        public Dictionary<string, PrinterStatus> PrinterStatus => _printerStatus;

        public void Initialize(ApplicationDbContext context)
        {
            foreach (var printer in context.Printers.ToList())
            {
                PrinterStatus[printer.Id] = new PrinterStatus();
            }

            IsReady = true;
        }
    }
}
