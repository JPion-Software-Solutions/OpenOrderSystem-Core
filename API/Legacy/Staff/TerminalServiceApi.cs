using System.Text;
using System.Text.Json;
using K4os.Hash.xxHash;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.API.Legacy.DTO;
using OpenOrderSystem.Core.API.Legacy.DTO.Factories;
using OpenOrderSystem.Core.API.Legacy.Models;
using OpenOrderSystem.Core.Areas.API.Legacy.Models;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Legacy;
using static OpenOrderSystem.Core.Areas.API.Legacy.Controllers.Staff.TerminalServiceController;

namespace OpenOrderSystem.Core.API.Legacy.Staff;

/// <summary>Legacy <c>api/legacy/Staff/TerminalService</c> endpoints (formerly TerminalServiceController). All authorized.</summary>
public static class TerminalServiceApi
{
    // GET
    public static IResult Alerts([FromServices] StaffTerminalMonitoringService staffTMS)
    {
        var newOrderAlert = staffTMS.NewOrderAlert;
        var timeLowAlert = staffTMS.OrderTimerAlert;
        var genericAlerts = new Dictionary<string, bool>();
        foreach (var alert in staffTMS.GenericTriggers.Keys)
        {
            //copys generic alerts and clears them from the queue.
            genericAlerts[alert] = staffTMS.CheckGenericTrigger(alert);
        }

        if (!newOrderAlert && !timeLowAlert && genericAlerts.Count == 0)
            return Results.NoContent();

        return Results.Ok(new
        {
            newOrderAlert,
            timeLowAlert,
            genericAlerts
        });
    }

    // PUT, form fields OrderId and Time
    public static async Task<IResult> AddOrderTime(HttpContext httpContext, [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = await binder.FormAsync<AddTimeModel>("model");
        if (binder.ApiControllerError() is { } error) return error;

        var orderId = model.OrderId;
        var time = model.Time;

        var order = context.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return Results.NotFound(new
            {
                errorMessage = $"Failed to locate order#: {orderId}."
            });

        order.AddToTimer(time);
        context.Orders.Update(order);
        context.SaveChanges();

        return Results.Ok(new
        {
            orderId,
            timeAdded = time,
            errorMessage = $"SUCCESS: Successfully added {time} minutes to order#: {orderId}"
        });
    }

    // PUT
    public static async Task<IResult> Ping(
        HttpContext httpContext,
        [FromServices] StaffTerminalMonitoringService staffTMS,
        [FromServices] ApplicationDbContext context,
        [FromServices] PrinterService printService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = (await binder.BodyAsync<PingDto>("model"))!;
        if (binder.ApiControllerError() is { } error) return error;

        staffTMS.RegisterCheckin();

        var orders = context.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.OrderPlaced)
            .Take(100)
            .ToArray();

        var inProgress = orders
            .AsEnumerable()
            .Where(o => o.StageLegacy == OrderStageLegacy.InProgress)
            .ToArray();

        foreach (var order in inProgress)
        {
            var timerStatus = order.CheckTimer();
            if (timerStatus == TimerStatus.TimeUp)
            {
                order.CompleteStage();
                context.Update(order);
                context.SaveChanges();
            }
        }

        var printer = context.Printers.FirstOrDefault(p => p.Id == model.Printer);

        if (printer == null)
            printer = context.Printers.FirstOrDefault(p => p.DefaultOrderPrinter);

        if (printer == null)
            throw new InvalidOperationException("Unable to locate any suitable printer!");

        var status = printService.GetStatus(printer.Id);
        var printerStatus = new
        {
            status.IsPaperLow,
            status.IsConnected,
            status.IsCoverOpen,
            status.IsErrorState,
            status.IsPaperOut,
            status.IsReady,
            status.IsBridgeActive
        };

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orders));
        var mid = (int)Math.Floor((decimal)bytes.Length / 2);

        var b1 = bytes.Take(mid + bytes.Length % 2).ToArray();
        var b2 = bytes.Skip(mid + bytes.Length % 2).ToArray();

        var h1 = XXH64.DigestOf(b1);
        var h2 = XXH64.DigestOf(b2);

        var printersBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(printerStatus));
        var printersHash = XXH64.DigestOf(printersBytes);

        var currentStateHash = $"{h1:X16}{h2:X16}:{printersHash:X16}";

        if (model.State == null || currentStateHash != model.State.Replace("\"", ""))
        {
            return Results.Ok(currentStateHash);
        }

        return Results.NoContent();
    }

    // GET
    public static IResult FetchPrinterStatus(
        HttpContext httpContext,
        [FromServices] ApplicationDbContext context,
        [FromServices] PrinterService printService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var printerId = binder.QueryString("printerId", required: false);
        if (binder.ApiControllerError() is { } error) return error;

        var printer = context.Printers.FirstOrDefault(p => p.Id == printerId);

        if (printer == null)
            printer = context.Printers.FirstOrDefault(x => x.DefaultOrderPrinter);

        if (printer == null)
            return Results.NotFound("Unable to locate a printer to poll.");

        return Results.Ok(printService.GetStatus(printer.Id));
    }

    // GET
    public static async Task<IResult> FetchOrders(
        [FromServices] ApplicationDbContext context,
        [FromServices] ITimeNormalizationService timeService)
    {
        var timezoneId = await timeService.GetLocalTimezoneId();
        var localTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, localTimeZone);

        var orders = context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Discount)
                .ThenInclude(d => d.WhiteListItemsVarients)
            .AsEnumerable()
            .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                o.OrderPlaced, localTimeZone).Date == currentTime.Date)
            .ToList();

        var sanitizedOrderList = new List<OrderResponse>();

        foreach (var order in orders)
        {
            order.LineItems = await context.OrderLines
            .Where(ol => ol.OrderId == order.Id)
            .Include(ol => ol.MenuItem)
                .ThenInclude(mi => mi.Ingredients)
            .Include(ol => ol.MenuItem)
                .ThenInclude(mi => mi.RawDbVarients)
            .Include(ol => ol.Ingredients)
            .ToListAsync();

            sanitizedOrderList.Add(OrderResponseFactory.Create(order));
        }

        return Results.Ok(sanitizedOrderList);
    }
}
