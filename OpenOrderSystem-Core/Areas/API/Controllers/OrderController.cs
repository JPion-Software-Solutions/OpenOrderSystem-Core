using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.Interfaces;
using PizzaPartry.tools;

namespace OpenOrderSystem.Core.Areas.API.Controllers
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

        [HttpGet]
        [Route("API/CheckOrder/{orderId}")]
        public IActionResult CheckStatus(int orderId)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
            {
                return NotFound($"failed to locate order#:{orderId}");
            }

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
                orderComplete = order.OrderComplete != null
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
        public IActionResult UpdateStatus([FromBody] UpdateStatusModel model)
        {
            var orderId = model.OrderId;

            var order = _context.Orders
                .Include(o => o.Customer)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
            {
                return NotFound($"failed to locate order#:{orderId}");
            }

            if (order.Stage == OrderStage.Ready)
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
            _context.SaveChanges();

            if (order.Customer != null)
                AlertCustomer(order.Customer, order.Stage);

            return new JsonResult(new
            {
                message = $"Order#{order.Id} advanced to stage {order.Stage}."
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
                    && o.Stage == OrderStage.InProgress)
                .ToList();

            foreach (var order in ordersInProgress)
            {
                if (order.CheckTimer() == TimerStatus.TimeUp)
                {
                    order.CompleteStage();
                    _context.Update(order);
                    _context.SaveChanges();

                    if (order.Customer != null)
                        AlertCustomer(order.Customer, order.Stage);
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

        private void AlertCustomer(Customer customer, OrderStage stage)
        {
            switch (stage)
            {
                case OrderStage.Ready:
                    if (customer.EmailUpdates)
                    {
                        _emailService.Send(
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
