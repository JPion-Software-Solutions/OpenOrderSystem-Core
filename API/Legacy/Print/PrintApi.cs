using ESCPOS_NET.Emitters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.API.Legacy.DTO.Factories;
using OpenOrderSystem.Core.Areas.API.Legacy.Models;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Services.Legacy;
using static OpenOrderSystem.Core.Areas.API.Legacy.Controllers.PrinterController;

namespace OpenOrderSystem.Core.API.Legacy.Print;

/// <summary>Legacy <c>api/legacy/Print</c> endpoints (formerly PrinterController). Authorized unless noted.</summary>
public static class PrintApi
{
    // GET, anonymous
    public static IResult Ping()
    {
        return Results.Ok("Open Order System - Printer Bridge Interface Active");
    }

    // POST Register
    public static async Task<IResult> Register(
        HttpContext httpContext,
        [FromServices] ApplicationDbContext context,
        [FromServices] PrinterService printerService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<PrinterRegistrationModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var printer = new Printer
        {
            Id = Guid.NewGuid().ToString(),
            Name = model.PrinterName
        };

        printer.SetPin(model.Pin);
        printer.SetClient(model.ClientId);

        var existing = context.Printers.FirstOrDefault(p => p.Client == printer.Client);
        if (existing != null)
            return Results.BadRequest($"Client {model.ClientId} already assigned to printer. Use API/Print/Unlink to " +
                $"unlink client first.");

        context.Printers.Add(printer);
        context.SaveChanges();

        printerService.AddPrinterTracking(printer);

        return Results.Ok(new
        {
            message = $"New printer '{printer.Name} added with id: {printer.Id}",
            printerId = printer.Id
        });
    }

    // PUT Register, anonymous
    public static async Task<IResult> RegisterExisting(HttpContext httpContext, [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<RegisterExistingPrinterModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var printer = context.Printers.FirstOrDefault(p => p.Id == model.PrinterId);
        if (printer == null || !printer.ValidatePin(model.Pin))
        {
            // The old action echoed its (by then valid, and therefore empty) ModelState with an added error.
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("PrinterId", "Unable to locate printer, please verify correct printerId and pin then try again.");
            return Results.NotFound(modelState);
        }

        //printer found and pin validated
        printer.SetClient(model.ClientId);
        printer.Name = model.PrinterName;
        context.SaveChanges();

        return Results.Ok(new
        {
            message = $"Printer '{printer.Name} with id: {printer.Id} registered with new host.",
            printerId = printer.Id
        });
    }

    // PUT CheckIn, anonymous, print bridge validated
    public static async Task<IResult> CheckIn(HttpContext httpContext, [FromServices] PrinterService printerService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<PrinterCheckInModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var status = printerService.GetStatus(model.PrinterId) ??
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

        printerService.UpdateStatus(model.PrinterId, model.ClientId, status);

        if (status.IsPaperOut || status.IsCoverOpen || status.IsErrorState ||
            !status.IsConnected)
            return Results.NoContent();


        var jobs = await printerService.CheckPrintQueue(model.PrinterId, model.ClientId);

        if (jobs == null) return Results.NoContent();

        var payload = new Dictionary<string, string>();
        foreach (var job in jobs)
        {
            payload[job.JobId] = job.PrintData;
        }

        return Results.Ok(payload);
    }

    // PUT CompleteJob, anonymous, print bridge validated
    public static async Task<IResult> CompleteJob(HttpContext httpContext, [FromServices] PrinterService printerService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<CompleteJobModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        printerService.RemoveJob(model.PrinterId, model.JobId);

        return Results.Accepted();
    }

    // POST
    public static async Task<IResult> PrintOrderTicket(
        HttpContext httpContext,
        [FromServices] ApplicationDbContext context,
        [FromServices] PrinterService printerService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<PrintOrderTicketModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        var bob = new PrintJobBuilder();
        Printer printer;
        PrintTemplate printTemplate;
        Order order;
        try
        {
            printer = context.Printers.FirstOrDefault(p => p.Id == model.PrinterId) ??
                context.Printers.FirstOrDefault(p => p.DefaultOrderPrinter) ??
                throw new ArgumentNullException($"Failed to locate printerId: {model.PrinterId}");

            printTemplate = context.PrintTemplates.FirstOrDefault(t => t.Id == model.TemplateId) ??
                context.PrintTemplates.FirstOrDefault(t => t.DefaultOrderTemplate) ??
                throw new ArgumentNullException($"Failed to locate templateId: {model.TemplateId}");

            order = context.Orders
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

        printerService.QueueJob(job);

        return Results.Accepted();
    }

    // POST
    public static async Task<IResult> TestPrint(HttpContext httpContext, [FromServices] PrinterService printerService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<TestPrintModel>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

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

        printerService.QueueJob(bob.Build(model.PrinterId));

        return Results.Ok();
    }
}
