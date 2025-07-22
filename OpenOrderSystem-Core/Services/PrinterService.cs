using ESCPOS_NET.Emitters;
using ESCPOS_NET.Emitters.BaseCommandValues;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;

namespace OpenOrderSystem.Core.Services
{
    public class PrinterService
    {
        private readonly ApplicationDbContext _context;
        private readonly PrintSpoolerService _printSpooler;

        public PrinterService(ApplicationDbContext context, PrintSpoolerService printSpooler)
        {
            _context = context;
            _printSpooler = printSpooler;

            if (!_printSpooler.IsReady) _printSpooler.Initialize(context);
        }

        public EPSON Printer { get; private set; } = new EPSON();

        public void AddPrinterTracking(Printer printer)
        {
            _printSpooler.PrinterStatus[printer.Id] = new PrinterStatus();
        }

        public PrinterStatus GetStatus(string printerId) => _printSpooler.PrinterStatus.ContainsKey(printerId) ?
            _printSpooler.PrinterStatus[printerId] : new PrinterStatus();

        public int UpdateStatus(string printerId, string clientId, PrinterStatus status)
        {
            var printer = _context.Printers.FirstOrDefault(x => x.Id == printerId);
            if (printer != null && printer.ValidateClient(clientId))
            {
                status.LastOnline = DateTime.UtcNow;
                _printSpooler.PrinterStatus[printerId] = status;

                return _printSpooler.PrinterStatus[printerId].JobsWaiting;
            }

            return -1;
        }

        public string? GetPrinterName(string printerId) => _context.Printers.FirstOrDefault(p => p.Id == printerId)?.Name;

        public async Task<List<PrintJob>?> CheckPrintQueue(string printerId, string clientId)
        {
            var printer = await _context.Printers.FirstOrDefaultAsync(x => x.Id == printerId);
            if (printer != null && printer.ValidateClient(clientId) &&
                _printSpooler.PrinterStatus[printerId].JobsWaiting > 0)
            {
                var jobs = new List<PrintJob>();

                foreach (var jobId in _printSpooler.PrinterStatus[printerId].Queue.Keys)
                {
                    if (_printSpooler.PrinterStatus[printerId].Queue[jobId].InProgress) continue;

                    _printSpooler.PrinterStatus[printerId].Queue[jobId].InProgress = true;
                    jobs.Add(_printSpooler.PrinterStatus[printerId].Queue[jobId]);
                }

                return jobs;
            }

            return null;
        }

        public void RemoveJob(string printerId, string jobId)
        {
            var printer = _context.Printers.FirstOrDefault(x => x.Id == printerId);
            if (printer != null)
            {
                _printSpooler.PrinterStatus[printerId].Queue.Remove(jobId);
            }
        }

        public bool QueueJob(PrintJob job)
        {
            //verify the printer exists.
            if (_context.Printers.FirstOrDefault(p => p.Id == job.PrinterId) != null)
            {
                _printSpooler.PrinterStatus[job.PrinterId].Queue.Add(job.JobId, job);
                return true;
            }

            return false;
        }
    }
}
