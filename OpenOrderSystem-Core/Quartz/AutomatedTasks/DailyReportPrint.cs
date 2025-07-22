using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Services;
using Quartz;

namespace OpenOrderSystem.Core.Quartz.AutomatedTasks
{
    public class DailyReportPrint : IJob
    {
        private readonly IServiceScopeFactory _serviceFactory;
        private readonly ILogger<DailyReportPrint> _logger;
        private readonly PrinterService _printService;

        public DailyReportPrint(IServiceScopeFactory serviceFactory, ILogger<DailyReportPrint> logger, PrinterService printService)
        {
            _serviceFactory = serviceFactory;
            _logger = logger;
            _printService = printService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Printing daily report.");

            var scope = _serviceFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var printer = await dbContext.Printers.FirstOrDefaultAsync(p => p.DefaultEndOfDayPrinter);
            var template = await dbContext.PrintTemplates.FirstOrDefaultAsync(t => t.DefaultEndOfDayTemplate);

            if (printer == null || template == null)
            {
                var issue = printer == null && template == null ? "printer or template" :
                    printer == null ? "printer" : "template";
                _logger.LogError($"Unable to print daily reports: Cannot locate default {issue} for printing daily report.");
                return;
            }

            var orders = dbContext.Orders;

            var report = EndOfDayReportBuilder.Create()
                .SetDate(DateTime.UtcNow)
                .UsingTimezone("America/New_York")
                .AddOrders(orders)
                .Build();

            var bob = new PrintJobBuilder();
            var job = bob
                .AddEndOfDayData(report)
                .UseTemplate(template)
                .Build(printer.Id);

            if (_printService.QueueJob(job))
                _logger.LogInformation($"Daily report print job ({job.JobId}) queued using printer {printer.Name} ({printer.Id})" +
                    $" and template {template.Name} ({template.Id})");
            else
                _logger.LogError($"Failed to queue daily report for printing. Printer: {printer.Name}/{printer.Id}. " +
                    $"Template: {template.Name}/{template.Id}");
        }
    }
}
