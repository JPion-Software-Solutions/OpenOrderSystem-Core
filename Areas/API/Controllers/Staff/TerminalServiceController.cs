using K4os.Hash.xxHash;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Areas.API.DTO;
using OpenOrderSystem.Core.Areas.API.DTO.Factories;
using OpenOrderSystem.Core.Areas.API.Models;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OpenOrderSystem.Core.Areas.API.Controllers.Staff
{
    [Area("API/Staff")]
    [ApiController]
    [Route("API/Staff/TerminalService/{action}")]

    [Authorize]
    public class TerminalServiceController : ControllerBase
    {
        private readonly StaffTerminalMonitoringService _staffTMS;
        private readonly ApplicationDbContext _context;
        private readonly PrinterService _printService;

        public TerminalServiceController(ApplicationDbContext context, StaffTerminalMonitoringService staffTMS,
            PrinterService printService)
        {
            _context = context;
            _staffTMS = staffTMS;
            _printService = printService;
        }

        [HttpGet]
        public IResult Alerts()
        {
            var newOrderAlert = _staffTMS.NewOrderAlert;
            var timeLowAlert = _staffTMS.OrderTimerAlert;
            var genericAlerts = new Dictionary<string, bool>();
            foreach (var alert in _staffTMS.GenericTriggers.Keys)
            {
                //copys generic alerts and clears them from the queue.
                genericAlerts[alert] = _staffTMS.CheckGenericTrigger(alert);
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

        [HttpPut]
        public IResult AddOrderTime([FromForm] AddTimeModel model)
        {
            var orderId = model.OrderId;
            var time = model.Time;

            var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
                return Results.NotFound(new
                {
                    errorMessage = $"Failed to locate order#: {orderId}."
                });

            order.AddToTimer(time);
            _context.Orders.Update(order);
            _context.SaveChanges();

            return Results.Ok(new
            {
                orderId,
                timeAdded = time,
                errorMessage = $"SUCCESS: Successfully added {time} minutes to order#: {orderId}"
            });
        }

        public class PingDto
        {
            public string State { get; set; } = string.Empty;
            public string? Printer { get; set; }
        }

        [HttpPut]
        [DisableRateLimiting]
        public IResult Ping([FromBody] PingDto model)
        {
            _staffTMS.RegisterCheckin();

            var orders = _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.OrderPlaced)
                .Take(100)
                .ToArray();

            var inProgress = orders
                .AsEnumerable()
                .Where(o => o.Stage == OrderStage.InProgress)
                .ToArray();

            foreach (var order in inProgress)
            {
                var timerStatus = order.CheckTimer();
                if (timerStatus == TimerStatus.TimeUp)
                {
                    order.CompleteStage();
                    _context.Update(order);
                    _context.SaveChanges();
                }
            }

            var printer = _context.Printers.FirstOrDefault(p => p.Id == model.Printer);

            if (printer == null)
                printer = _context.Printers.FirstOrDefault(p => p.DefaultOrderPrinter);

            if (printer == null)
                throw new InvalidOperationException("Unable to locate any suitable printer!");

            var status = _printService.GetStatus(printer.Id);
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

        [HttpGet]
        public IResult FetchPrinterStatus(string? printerId)
        {
            var printer = _context.Printers.FirstOrDefault(p => p.Id == printerId);

            if (printer == null)
                printer = _context.Printers.FirstOrDefault(x => x.DefaultOrderPrinter);

            if (printer == null)
                return Results.NotFound("Unable to locate a printer to poll.");

            return Results.Ok(_printService.GetStatus(printer.Id));
        }

        [HttpGet]
        public async Task<IResult> FetchOrdersAsync()
        {
            var utcTime = DateTime.UtcNow;
            TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var localTimeZone);
            var currentTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, localTimeZone ?? TimeZoneInfo.Local);

            var orders = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Discount)
                    .ThenInclude(d => d.WhiteListItemsVarients)
                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                    o.OrderPlaced, localTimeZone ?? TimeZoneInfo.Local).Date == currentTime.Date)
                //.OrderByDescending(o => o.OrderPlaced)
                .ToList();

            var sanitizedOrderList = new List<OrderResponse>();

            foreach (var order in orders)
            {
                order.LineItems = await _context.OrderLines
                .Where(ol => ol.OrderId == order.Id)
                .Include(ol => ol.MenuItem)
                    .ThenInclude(mi => mi.Ingredients)
                .Include(ol => ol.MenuItem)
                    .ThenInclude(mi => mi.RawDbVarients)
                .Include(ol => ol.Ingredients)
                .ToListAsync();

                var sanitized = OrderResponseFactory.Create(order);

                //sanitized.Id = order.Id;
                //sanitized.OrderPlaced = order.OrderPlaced;
                //sanitized.OrderInprogress = order.OrderInprogress;
                //sanitized.OrderReady = order.OrderReady;
                //sanitized.OrderComplete = order.OrderComplete;
                //sanitized.Customer = order.Customer;

                //if (order.Discount != null)
                //{
                //    order.Discount.Orders = null;

                //    for (int i = 0; i < order.Discount.WhiteListItemsVarients?.Count; i++)
                //    {
                //        order.Discount.WhiteListItemsVarients[i].DiscountCodes = null;
                //        order.Discount.WhiteListItemsVarients[i].MenuItem = null;
                //    }
                //}

                //sanitized.Discount = order.Discount;

                //if (order.LineItems != null)
                //{
                //    for (int i = 0; i < order.LineItems.Count; i++)
                //    {
                //        order.LineItems[i].Order = null;

                //        for (int j = 0; j < order.LineItems[i].Ingredients?.Count; j++)
                //        {
                //            if (order.LineItems[i] != null && order.LineItems[i].Ingredients != null)
                //            {
                //                order.LineItems[i].Ingredients![j].OrderLines = null;
                //                order.LineItems[i].Ingredients![j].MenuItems = null;
                //                order.LineItems[i].Ingredients![j].Category = null;
                //                order.LineItems[i].Ingredients![j].ProductCategories = null;
                //            }
                //        }

                //        if (order.LineItems[i].MenuItem != null)
                //        {
                //            order.LineItems[i].MenuItem!.OrderLines = null;
                //            order.LineItems[i].MenuItem!.ProductCategory = null;
                //            order.LineItems[i].MenuItem!.DiscountCodesItems = null;

                //            if (order.LineItems[i].MenuItem!.MenuItemVarients != null)
                //            {
                //                for (int j = 0; j < order.LineItems[i].MenuItem!.MenuItemVarients.Count; j++)
                //                {
                //                    order.LineItems[i].MenuItem!.MenuItemVarients[j].MenuItem = null; ;
                //                    order.LineItems[i].MenuItem!.MenuItemVarients[j].DiscountCodes = null;
                //                }
                //            }

                //            if (order.LineItems[i].MenuItem!.Ingredients != null)
                //            {
                //                for (int j = 0; j < order.LineItems[i].MenuItem!.Ingredients!.Count; j++)
                //                {
                //                    order.LineItems[i].MenuItem!.Ingredients![j].MenuItems = null;
                //                    order.LineItems[i].MenuItem!.Ingredients![j].Category = null;
                //                    order.LineItems[i].MenuItem!.Ingredients![j].ProductCategories = null;
                //                    order.LineItems[i].MenuItem!.Ingredients![j].OrderLines = null;
                //                }
                //            }
                //        }
                //    }
                //}

                //sanitized.LineItems = order?.LineItems ?? new List<OrderLine>();

                sanitizedOrderList.Add(sanitized);
            }

            return Results.Ok(sanitizedOrderList);
        }
    }
}
