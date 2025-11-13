using Azure.Core.Serialization;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Areas.API.DTO.Factories;
using OpenOrderSystem.Core.Areas.API.Models;
using OpenOrderSystem.Core.Attributes;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Services;
using System.Reflection;
using System.Text.Json;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{
    [Area("API")]
    [ApiController]
    [Route("API/Print/{action}")]
    [Authorize]
    public class PrinterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PrinterService _printerService;

        public PrinterController(ApplicationDbContext context, PrinterService printerService)
        {
            _context = context;
            _printerService = printerService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IResult Ping()
        {
            return Results.Ok("Open Order System - Printer Bridge Interface Active");
        }

        [HttpPost]
        public IResult Register(PrinterRegistrationModel model)
        {
            if (ModelState.IsValid)
            {
                var printer = new Printer
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = model.PrinterName
                };

                printer.SetPin(model.Pin);
                printer.SetClient(model.ClientId);

                var existing = _context.Printers.FirstOrDefault(p => p.Client == printer.Client);
                if (existing != null)
                    return Results.BadRequest($"Client {model.ClientId} already assigned to printer. Use API/Print/Unlink to " +
                        $"unlink client first.");

                _context.Printers.Add(printer);
                _context.SaveChanges();

                _printerService.AddPrinterTracking(printer);

                return Results.Ok(new
                {
                    message = $"New printer '{printer.Name} added with id: {printer.Id}",
                    printerId = printer.Id
                });
            }

            return Results.BadRequest(ModelState);
        }

        public class RegisterExistingPrinterModel : PrinterRegistrationModel
        {
            public string PrinterId { get; set; } = string.Empty;
        }

        [HttpPut]
        [AllowAnonymous]
        public IResult Register(RegisterExistingPrinterModel model)
        {
            if (ModelState.IsValid)
            {
                var printer = _context.Printers.FirstOrDefault(p => p.Id == model.PrinterId);
                if (printer == null || !printer.ValidatePin(model.Pin))
                {
                    ModelState.AddModelError("PrinterId", "Unable to locate printer, please verify correct printerId and pin then try again.");
                    return Results.NotFound(ModelState);
                }

                //printer found and pin validated
                printer.SetClient(model.ClientId);
                printer.Name = model.PrinterName;
                _context.SaveChanges();

                return Results.Ok(new
                {
                    message = $"Printer '{printer.Name} with id: {printer.Id} registered with new host.",
                    printerId = printer.Id
                });
            }

            return Results.BadRequest(ModelState);
        }

        public class PrinterCheckInModel
        {
            public string PrinterId { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;

            public PrinterStatusEventArgs? Status { get; set; }
        }

        [HttpPut]
        [AllowAnonymous]
        [ValidatePrintBridge]
        [DisableRateLimiting]
        public async Task<IResult> CheckInAsync(PrinterCheckInModel model)
        {
            var status = _printerService.GetStatus(model.PrinterId) ??
                throw new ArgumentNullException("This should be impossible as the printerId is validated by" +
                "the PrinterBridgeAuth middleware before reaching this method.");

            status.LastOnline = DateTime.UtcNow;
            status.IsConnected = model.Status != null;

            if (model.Status != null)
            {
                status.IsConnected = model.Status.IsPrinterOnline ?? false;
                status.IsPaperOut = model.Status.IsPaperOut ?? false;
                status.IsPaperLow = model.Status.IsPaperLow ?? false;
                status.IsCoverOpen = model.Status.IsCoverOpen ?? false;
                status.IsErrorState = model.Status.IsInErrorState ?? false;
            }
            else
            {
                return Results.NoContent();
            }

            _printerService.UpdateStatus(model.PrinterId, model.ClientId, status);

            if (status.IsPaperOut || status.IsCoverOpen || status.IsErrorState ||
                !status.IsConnected)
                return Results.NoContent();


            var jobs = await _printerService.CheckPrintQueue(model.PrinterId, model.ClientId);

            if (jobs == null) return Results.NoContent();

            var payload = new Dictionary<string, string>();
            foreach (var job in jobs)
            {
                payload[job.JobId] = job.PrintData;
            }

            return Results.Ok(payload);
        }

        public class CompleteJobModel
        {
            public string JobId { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;
            public string PrinterId { get; set; } = string.Empty;
        }

        [HttpPut]
        [AllowAnonymous]
        [ValidatePrintBridge]
        public IResult CompleteJob(CompleteJobModel model)
        {
            _printerService.RemoveJob(model.PrinterId, model.JobId);

            return Results.Accepted();
        }

        public class PrintOrderTicketModel
        {
            public int OrderId { get; set; }

            public string? PrinterId { get; set; } = string.Empty;

            public string? TemplateId { get; set; } = string.Empty;
        }

        [HttpPost]
        public IResult PrintOrderTicket(PrintOrderTicketModel model)
        {
            var bob = new PrintJobBuilder();
            Printer printer;
            PrintTemplate printTemplate;
            Order order;
            try
            {
                printer = _context.Printers.FirstOrDefault(p => p.Id == model.PrinterId) ??
                    _context.Printers.FirstOrDefault(p => p.DefaultOrderPrinter) ??
                    throw new ArgumentNullException($"Failed to locate printerId: {model.PrinterId}");

                printTemplate = _context.PrintTemplates.FirstOrDefault(t => t.Id == model.TemplateId) ??
                    _context.PrintTemplates.FirstOrDefault(t => t.DefaultOrderTemplate) ??
                    throw new ArgumentNullException($"Failed to locate templateId: {model.TemplateId}");

                order = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.LineItems)
                    .Include(o => o.LineItems)
                        .ThenInclude(li => li.Ingredients)
                    .Include(o => o.LineItems)
                        .ThenInclude(li => li.MenuItem)
                    .Include(o => o.LineItems)
                        .ThenInclude(li => li.MenuItem)
                            .ThenInclude(mi => mi.RawDbVarients)
                    .Include(o => o.LineItems)
                        .ThenInclude(li => li.MenuItem)
                            .ThenInclude(mi => mi.Ingredients)
                    .Include(o => o.Discount)
                    .FirstOrDefault(o => o.Id == model.OrderId) ??
                        throw new ArgumentNullException($"Failed to locate order#: {model.OrderId}");
            }
            catch (ArgumentNullException ex)
            {
                return Results.NotFound(ex.Message);
            }

            var job = bob
                .UseTemplate(printTemplate)
                .AddOrderData(OrderResponseFactory.Create(order))
                .Build(printer.Id);

            _printerService.QueueJob(job);

            return Results.Accepted();
        }

        public class TestPrintModel
        {
            public string PrinterId { get; set; } = string.Empty;
        }

        [HttpPost]
        public IResult TestPrint([FromBody] TestPrintModel model)
        {
            var bob = new PrintJobBuilder();
            var e = new EPSON();

            bob.AddInstruction(e.CenterAlign())
                .AddInstruction(e.SetStyles(PrintStyle.DoubleHeight))
                .AddInstruction(e.SetStyles(PrintStyle.DoubleWidth))
                .AddInstruction(e.PrintLine("Print Test"))
                .AddInstruction(e.SetStyles(PrintStyle.None))
                .AddInstruction(e.FeedLines(10))
                .AddInstruction(e.LeftAlign())
                .AddInstruction(e.PrintLine("Normal Text"))
                .AddInstruction(e.PrintBarcode(BarcodeType.UPC_A, "88899991234"))
                .AddInstruction(e.Print2DCode(TwoDimensionCodeType.QRCODE_MODEL1, "Hello World"))
                .AddInstruction(e.PartialCutAfterFeed(10));

            _printerService.QueueJob(bob.Build(model.PrinterId));

            return Results.Ok();
        }
    }
}
