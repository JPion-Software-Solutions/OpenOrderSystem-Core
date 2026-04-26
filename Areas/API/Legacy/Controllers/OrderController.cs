using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;
using PizzaPartry.tools;

namespace OpenOrderSystem.Core.Areas.API.Legacy.Controllers
{
    [Area("API")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StaffTerminalMonitoringService _staffTMS;
        private readonly ConfigurationService _config;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;

        public OrderController(ApplicationDbContext context, StaffTerminalMonitoringService staffTMS,
            ConfigurationService config, IEmailService emailService, ISmsService smsService)
        {
            _context = context;
            _staffTMS = staffTMS;
            _config = config;
            _emailService = emailService;
            _smsService = smsService;
        }

        public enum LocateMethod
        {
            OrderId,
            Phone
        }
        [HttpGet]
        [Route("/API/Order/Locate/{method}/{key}")]
        public async Task<IResult> Locate(string key, LocateMethod method = LocateMethod.OrderId)
        {
            var orders = Array.Empty<int>();
            if (method == LocateMethod.OrderId)
            {
                if (int.TryParse(key, out var parsedKey))
                {
                    var temp = await _context.Orders
                        .Where(o => o.Id == parsedKey)
                        .ToListAsync();

                    orders = temp
                        .Select(o => o.Id)
                        .ToArray();
                }
            }
            else
            {

                var temp = await _context.Orders
                    .Include(o => o.Customer)
                    .Where(o => o.Customer != null && o.Customer.Phone == key)
                    .ToListAsync();

                orders = temp
                    .Select(o => o.Id)
                    .ToArray();
            }

            if (orders.Any())
            {
                if (orders.Length == 1)
                {
                    return Results.Ok(new
                    {
                        ordersFound = orders.Length,
                        resultMsg = $"Found 1 order using {method}:{key}",
                        orderId = orders[0]
                    });
                }
                else
                {
                    return Results.Ok(new
                    {
                        ordersFound = orders.Length,
                        resultMsg = $"Found {orders.Length} orders using {method}:{key}. Multiple matches — frontend must disambiguate.",
                        orders
                    });
                }
            }

            return Results.NotFound(new
            {
                ordersFound = 0,
                resultMsg = $"No orders were found using the provided information ({method}:{key})."
            });
        }

        [HttpGet]
        [Route("API/CheckOrder/{orderId}")]
        public IActionResult CheckStatus(int orderId)
        {
            return CheckStatusTemp(orderId);
            //var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
            //if (order == null)
            //{
            //    return NotFound($"failed to locate order#:{orderId}");
            //}

            //return new JsonResult(new
            //{
            //    //get times
            //    orderRecievedTime = order.OrderPlaced,
            //    orderInProgressTime = order.OrderInprogress,
            //    orderReadyTime = order.OrderReady,
            //    orderCompleteTime = order.OrderComplete,

            //    //check stages
            //    orderInProgress = order.OrderInprogress != null,
            //    orderReady = order.OrderReady != null,
            //    orderComplete = order.OrderComplete != null
            //});
        }
        private IActionResult CheckStatusTemp(int orderId)
        {
            // First fetch: simple existence + timestamps
            var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound($"failed to locate order#:{orderId}");
            }

            // Second fetch: light detail load for the status page
            var orderDetails = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.LineItems)
                    .ThenInclude(li => li.MenuItem)
                        .ThenInclude(mi => mi!.RawDbVarients)
                .FirstOrDefault(o => o.Id == orderId);

            _context.OrderLines
                .Include(ol => ol.Ingredients)
                .Include(ol => ol.MenuItem)
                    .ThenInclude(mi => mi.Ingredients)
                .Load();

            // Build a light detail object (not the massive staff version)
            var details = new
            {
                subtotal = orderDetails?.Subtotal.ToString("C") ?? "",
                tax = orderDetails?.Tax.ToString("C") ?? "",
                total = orderDetails?.Total.ToString("C") ?? "",
                lineItems = orderDetails?.LineItems.Select(li => new {
                    name = li.MenuItem?.Name ?? "",
                    variant = li.MenuItem?.MenuItemVarients?[li.MenuItemVarient]?.Descriptor ?? "",
                    comments = li.LineComments ?? "",
                    price = li.LinePrice.ToString("C"),
                    ingAdded = li.AddedIngredients.Select(ai => new 
                    {
                        name = ai.Name
                    }),
                    ingRemoved = li.RemovedIngredients.Select(ri => new
                    {
                        name = ri.Name
                    })
                })
            };

            return new JsonResult(new
            {
                //get times
                orderRecievedTime = order.OrderPlaced,
                orderInProgressTime = order.OrderInprogress,
                orderReadyTime = order.OrderReady,
                orderCompleteTime = order.OrderComplete,

                //check stages
                orderInProgress = order.OrderInprogress != null,
                orderReady = order.OrderReady != null,
                orderComplete = order.OrderComplete != null,

                //NEW: lightweight payload for the status page
                details
            });
        }

        [HttpGet]
        public bool IsOpen() =>
            _staffTMS.TerminalActive &&         //verifys the staff terminal hasn't lost connection
            _config.Settings.AcceptingOrders;   //verifys time within scheduled ordering hours

        [HttpGet]
        [Authorize]
        [Route("/API/Staff/Orders/Detail/{id}")]
        public IResult Detail(int id)
        {
            var order = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.LineItems)
                    .ThenInclude(ol => ol.Ingredients)
                .Include(o => o.LineItems)
                    .ThenInclude(ol => ol.MenuItem)
                        .ThenInclude(mi => mi.RawDbVarients)
                .Include(o => o.LineItems)
                    .ThenInclude(ol => ol.MenuItem)
                        .ThenInclude(mi => mi.Ingredients)
                .Include(o => o.Discount)
                .FirstOrDefault(o => o.Id == id);

            if (order != null)
            {
                var fancyAssPhoneNumber = "(";
                var phone = order.Customer?.Phone ?? "Error Retrieving Phone";
                for (var k = 0; k < phone.Length; ++k)
                {
                    var d = phone[k];
                    if (k == 2)
                        fancyAssPhoneNumber += $"{d})";
                    else if (k == 5)
                        fancyAssPhoneNumber += $"{d}-";
                    else
                        fancyAssPhoneNumber += d;
                }

                var algorithm = new CheckDigitCalc.WeightingFactor[]
                {
                        CheckDigitCalc.WeightingFactor.TwoMinus,
                        CheckDigitCalc.WeightingFactor.TwoMinus,
                        CheckDigitCalc.WeightingFactor.Three,
                        CheckDigitCalc.WeightingFactor.FiveMinus
                };

                var comboUPC = order.Subtotal > 99.99 ? "" : CheckDigitCalc.Create(order.Subtotal
                    .ToString("C")
                    .Replace("$", "")
                    .Replace(".", "")
                    .Replace(" ", "")
                    .PadLeft(4, '0'), algorithm)
                    .GetResult();

                var details = new
                {
                    orderNum = order.Id,
                    customerName = order.Customer?.Name ?? string.Empty,
                    customerPhone = fancyAssPhoneNumber ?? string.Empty,
                    promo = new
                    {
                        code = order.DiscountId,
                        initialSubtotal = order.LineItemTotal,
                        initialSubtotalStr = order.LineItemTotal.ToString("C"),
                        discount = order.Discount?.GetDiscount(order),
                        discountStr = $"-{order.Discount?.GetDiscount(order).ToString("C")}"
                    },
                    subtotal = order.Subtotal.ToString("C"),
                    tax = order.Tax.ToString("C"),
                    total = order.Total.ToString("C"),
                    lineItems = new List<object>(),
                    comboUpc = order.Subtotal > 99.99 ? "" : $"207001{comboUPC}",
                    comboExcessAmnt = order.Subtotal > 99.99
                };

                foreach (var item in order.LineItems)
                {
                    var additions = new List<object>();
                    foreach (var add in item.AddedIngredients)
                        additions.Add(new
                        {
                            name = add.Name,
                            price = add.Price
                        });

                    var subtractions = new List<object>();
                    foreach (var sub in item.RemovedIngredients)
                        subtractions.Add(new
                        {
                            name = sub.Name,
                            price = sub.Price
                        });

                    var barcodePrice = CheckDigitCalc.Create(item.LinePrice
                        .ToString("C")
                        .Replace("$", "")
                        .Replace(".", "")
                        .Replace(" ", "")
                        .PadLeft(4, '0'), algorithm)
                        .GetResult();

                    details.lineItems.Add(new
                    {
                        name = item.MenuItem?.Name ?? string.Empty,
                        varient = item.MenuItem?.MenuItemVarients?[item.MenuItemVarient]?.Descriptor ?? string.Empty,
                        additions,
                        subtractions,
                        modified = additions.Any() || subtractions.Any(),
                        comments = item.LineComments ?? string.Empty,
                        price = item.LinePrice,
                        plu = item.MenuItem?.MenuItemVarients?[item.MenuItemVarient]?.Upc ?? "00000",
                        upc = "2" + (item.MenuItem?.MenuItemVarients?[item.MenuItemVarient]?.Upc ?? "00000") + barcodePrice,
                        //upcDiscounted = "2" + (item.MenuItem?.MenuItemVarients?[item.MenuItemVarient]?.Upc ?? "00000") + barcodePriceDiscounted
                    });
                }

                return Results.Ok(details);
            }

            return Results.NotFound();
        }

        public class UpdateStatusModel
        {
            public int OrderId { get; set; }
        }

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusModel model)
        {
            var orderId = model.OrderId;

            var order = _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound($"failed to locate order#:{orderId}");
            }

            if (order.StageLegacy == OrderStageLegacy.Ready)
            {
                order = _context.Orders
                    .Include(o => o.Customer)
                    .Include(o => o.Discount)
                        .ThenInclude(d => d.WhiteListItemsVarients)
                    .Include(o => o.LineItems)
                        .ThenInclude(l => l.Ingredients)
                    .Include(o => o.LineItems)
                        .ThenInclude(l => l.MenuItem)
                            .ThenInclude(m => m.RawDbVarients)
                    .Include(o => o.LineItems)
                        .ThenInclude(l => l.MenuItem)
                            .ThenInclude(m => m.Ingredients)
                    .AsSplitQuery()
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                {
                    return NotFound($"failed to locate order#:{orderId}");
                }
            }

            order.CompleteStage();
            await _context.SaveChangesAsync();

            if (order.Customer != null)
                //TODO Replace direct email service usage with abstracted CustomerNotificationService
                await AlertCustomer(order.Customer, order.StageLegacy);

            return new JsonResult(new
            {
                message = $"Order#{order.Id} advanced to stage {order.StageLegacy}."
            });
        }

        [HttpPut]
        [Authorize]
        public void TerminalCheckin()
        {
            _staffTMS.RegisterCheckin();
            var ordersInProgress = _context.Orders
                .Include(o => o.Customer)
                .AsEnumerable()
                .Where(o => o.OrderPlaced.Date == DateTime.UtcNow.Date
                    && o.StageLegacy == OrderStageLegacy.InProgress)
                .ToList();

            foreach (var order in ordersInProgress)
            {
                if (order.CheckTimer() == TimerStatus.TimeUp)
                {
                    order.CompleteStage();
                    _context.Update(order);
                    _context.SaveChanges();

                    if (order.Customer != null)
                        AlertCustomer(order.Customer, order.StageLegacy);
                }
                if (order.CheckTimer() == TimerStatus.LessThanTwo)
                {
                    _staffTMS.TriggerOrderTimerAlert();
                }
            }
        }

        [HttpPut]
        [Authorize]
        public void TerminalClose() => _staffTMS.CloseTerminal();

        private async Task AlertCustomer(Customer customer, OrderStageLegacy stageLegacy)
        {
            switch (stageLegacy)
            {
                case OrderStageLegacy.Ready:
                    if (customer.EmailUpdates)
                    {
                        await _emailService.SendAsync(
                            customer.Email,
                            "Village Market Pizza Order",
                            "Your order is ready for pickup. Thank you for ordering from Village Market.");
                    }

                    if (customer.SMSUpdates)
                    {
                        var phone = _smsService.ConvertPhone(customer.Phone);
                        _smsService.SendSMS(phone, "Your order is ready for pickup. Thank " +
                            "you for ordering from Village Market Rapid City.");
                    }
                    break;
            }
        }

        [HttpDelete]
        [Authorize]
        [Route("API/Order/CancelOrder")]
        public IResult CancelOrder([FromBody] UpdateStatusModel model)
        {
            int orderNumber = model.OrderId;
            var order = _context.Orders.FirstOrDefault(o => o.Id == orderNumber);
            if (order == null)
                return Results.NotFound(new
                {
                    OrderId = orderNumber,
                    Message = $"Unable to locate order #{orderNumber}."
                });

            _context.Orders.Remove(order);
            _context.SaveChanges();

            return Results.Ok(new
            {
                Message = $"Order #{orderNumber} successsfully canceled"
            });
        }
    }
}
