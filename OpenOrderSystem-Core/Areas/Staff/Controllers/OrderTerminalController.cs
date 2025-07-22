using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Areas.Staff.Models;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Areas.API.Models;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.OrderTerminal;
using OpenOrderSystem.Core.Models;
using System.Net.Http.Headers;
using System.Text.Json.Serialization.Metadata;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers
{
    [Area("Staff")]
    [Authorize]
    public class OrderTerminalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly StaffTerminalMonitoringService _staffTMS;
        private readonly CartService _cartService;

        public OrderTerminalController(ApplicationDbContext context, SignInManager<IdentityUser> signInManager,
            StaffTerminalMonitoringService staffTMS, CartService cartService)
        {
            _context = context;
            _signInManager = signInManager;
            _staffTMS = staffTMS;
            _cartService = cartService;

            _context.DiscountCodes
                .Include(d => d.WhiteListItemsVarients)
                .Include(d => d.Orders)
                .Load();
        }
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [Route("/Staff/OrderTerminal/FetchOrderList/{stage}")]
        public IActionResult FetchOrderList(int stage = 0)
        {
            var utcTime = DateTime.UtcNow;
            TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var localTimeZone);
            var currentTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, localTimeZone ?? TimeZoneInfo.Local);

            var model = new OrderListVM();
            _context.OrderLines
                .Include(ol => ol.Ingredients)
                .Load();
            _context.MenuItems
                .Include(mi => mi.Ingredients)
                .Load();
            _context.MenuItemVarients.Load();
            _context.Ingredients.Load();
            var ordersToday = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.LineItems)

                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                    o.OrderPlaced, localTimeZone ?? TimeZoneInfo.Local).Date == currentTime.Date);

            if (ordersToday == null || !ordersToday.Any())
                ordersToday = new List<Order>().AsQueryable();

            switch (stage)
            {
                case 0:
                    model.Stage = OrderStage.Recieved;
                    model.Orders = ordersToday
                        .AsEnumerable()
                        .Where(o => o.Stage == OrderStage.Recieved)
                        .ToList();
                    model.EnabledButtons[OrderTerminalButtons.Info] = true;
                    model.EnabledButtons[OrderTerminalButtons.Next] = true;
                    model.EnabledButtons[OrderTerminalButtons.Print] = true;
                    model.EnabledButtons[OrderTerminalButtons.Cancel] = true;
                    model.EnabledButtons[OrderTerminalButtons.Edit] = true;
                    model.ShowAllInfo = true;
                    model.NullMessage = "*** no orders waiting in queue ***";
                    break;

                case 1:
                    model.Stage = OrderStage.InProgress;
                    model.Orders = ordersToday
                        .AsEnumerable()
                        .Where(o => o.Stage == OrderStage.InProgress)
                        .ToList();
                    model.EnabledButtons[OrderTerminalButtons.Info] = true;
                    model.EnabledButtons[OrderTerminalButtons.Timer] = true;
                    model.EnabledButtons[OrderTerminalButtons.Print] = true;
                    model.EnabledButtons[OrderTerminalButtons.Next] = true;
                    model.EnabledButtons[OrderTerminalButtons.Cancel] = true;
                    model.EnabledButtons[OrderTerminalButtons.Edit] = true;
                    model.NullMessage = "*** no orders in progress ***";
                    break;

                case 2:
                    model.Stage = OrderStage.Ready;
                    model.Orders = ordersToday
                        .AsEnumerable()
                        .Where(o => o.Stage == OrderStage.Ready)
                        .ToList();
                    model.EnabledButtons[OrderTerminalButtons.Info] = true;
                    model.EnabledButtons[OrderTerminalButtons.Print] = true;
                    model.EnabledButtons[OrderTerminalButtons.Done] = true;
                    model.EnabledButtons[OrderTerminalButtons.Edit] = true;
                    model.NullMessage = "*** no orders ready for pickup ***";
                    break;

                case 3:
                default:
                    model.Stage = OrderStage.Complete;
                    model.Orders = ordersToday
                        .AsEnumerable()
                        .Where(o => o.Stage == OrderStage.Complete)
                        .ToList();
                    model.EnabledButtons[OrderTerminalButtons.Info] = true;
                    model.EnabledButtons[OrderTerminalButtons.Print] = true;
                    model.EnabledButtons[OrderTerminalButtons.Edit] = true;
                    model.NullMessage = "*** no completed orders ***";
                    break;
            }

            return PartialView("_OrdersListPartial", model);
        }

        [HttpGet]
        public IActionResult FetchTerminalHeader()
        {
            var utcTime = DateTime.UtcNow;
            TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var localTimeZone);
            var currentTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, localTimeZone ?? TimeZoneInfo.Local);

            var model = new OrderHeaderVM();

            model.Recieved = _context.Orders
                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                    o.OrderPlaced, localTimeZone ?? TimeZoneInfo.Local).Date ==
                    currentTime.Date && o.Stage == OrderStage.Recieved)
                .ToList()
                .Count;

            model.InProgress = _context.Orders
                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                    o.OrderPlaced, localTimeZone ?? TimeZoneInfo.Local).Date ==
                    currentTime.Date && o.Stage == OrderStage.InProgress)
                .ToList()
                .Count;

            model.Ready = _context.Orders
                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                    o.OrderPlaced, localTimeZone ?? TimeZoneInfo.Local).Date ==
                    currentTime.Date && o.Stage == OrderStage.Ready)
                .ToList()
                .Count;

            model.Complete = _context.Orders
                .AsEnumerable()
                .Where(o => TimeZoneInfo.ConvertTimeFromUtc(
                    o.OrderPlaced, localTimeZone ?? TimeZoneInfo.Local).Date ==
                    currentTime.Date && o.Stage == OrderStage.Complete)
                .ToList()
                .Count;

            return PartialView("_OrderHeaderPartial", model);
        }

        [HttpGet("/Staff/OrderTerminal/Edit/{orderId}")]
        public IActionResult EditOrder(int orderId)
        {
            _context.Customers.Load();
            _context.MenuItemVarients.Load();

            _context.ProductCategories
                .Include(pc => pc.Ingredients)
                .Load();

            _context.MenuItems
                .Include(mi => mi.Ingredients)
                .Include(mi => mi.ProductCategory)
                .Load();

            _context.OrderLines
                .Include(l => l.Ingredients)
                .Include(l => l.MenuItem)
                .Load();

            _context.DiscountCodes.Load();

            var order = _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.LineItems)
                .Include(o => o.Discount)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null)
                return NotFound();

            _context.Customers.Load();
            _context.MenuItemVarients.Load();

            _context.MenuItems
                .Load();

            _context.OrderLines
                .Include(l => l.Ingredients)
                .Include(l => l.MenuItem)
                .Load();

            _context.DiscountCodes.Load();

            var cartId = _cartService.ProvisionCartFromExistingOrder(order);

            return RedirectToAction(nameof(WriteTicket), new { cartId });
        }

        [HttpGet]
        public async Task<IResult> FetchEndOfDayReport(string? filter)
        {
            var utcTime = DateTime.UtcNow;
            TimeZoneInfo.TryFindSystemTimeZoneById("Eastern Standard Time", out var localTimeZone);
            var reportDate = TimeZoneInfo.ConvertTimeFromUtc(utcTime, localTimeZone ?? TimeZoneInfo.Local);


            if (filter?.ToLower().Contains("date:") ?? false)
            {
                int index = 0;
                for (index = filter.ToLower().IndexOf("date:"); index < filter.Length; ++index)
                {
                    if (filter[index] == '!') break;
                }

                var dateStr = filter
                    .ToLower()
                    .Substring(filter.ToLower().IndexOf("date:"), index)
                    .Replace("date:", "");

                DateTime date;

                if (DateTime.TryParse(dateStr, out date))
                    reportDate = date;
            }

            var orders = await _context.Orders
                .Where(o => o.OrderPlaced.Date == reportDate.Date)
                .ToListAsync();

            var salesReport = new Dictionary<string, SalesData>();
            var promoReport = new SalesData();

            foreach (var order in orders)
            {
                if (order.LockedOrderDetail == null) continue;

                foreach (var line in order.LockedOrderDetail.LineItems)
                {
                    var a = line.Key.Split(" - ");

                    if (a.Length != 2) throw new ArgumentNullException();

                    var itemVarient = a[0];
                    var itemName = a[1];

                    var varientSales = salesReport.ContainsKey(itemName) ?
                        salesReport[itemName] : new SalesData();

                    if (varientSales.VarientSalesData.ContainsKey(itemVarient))
                    {
                        varientSales.VarientSalesData[itemVarient].Qty++;
                        varientSales.VarientSalesData[itemVarient].Sales += line.Value;
                    }

                    else
                    {
                        varientSales.VarientSalesData[itemVarient] = new VarientSalesData
                        {
                            Qty = 1,
                            Sales = line.Value,
                        };
                    }

                    salesReport[itemName] = varientSales;
                }

                if (!string.IsNullOrEmpty(order.LockedOrderDetail.PromoCode))
                {
                    var key = order.LockedOrderDetail.PromoCode;
                    var varientSales = salesReport.ContainsKey(key) ?
                        salesReport[key] : new SalesData();

                    if (promoReport.VarientSalesData.ContainsKey(key))
                    {
                        promoReport.VarientSalesData[key].Qty++;
                        promoReport.VarientSalesData[key].Sales -= order.LockedOrderDetail.Discount;
                    }
                    else
                    {
                        promoReport.VarientSalesData[key] = new VarientSalesData
                        {
                            Qty = 1,
                            Sales = order.LockedOrderDetail.Discount
                        };
                    }
                }

            }

            var grossSales = 0.0f;
            foreach (var saleData in salesReport.Values)
                grossSales += saleData.Sales;

            var report = new EndOfDayReport
            {
                Date = reportDate.Date,
                OrderCount = orders.Count,
                GrossSales = grossSales,
                SalesReport = salesReport,
                PromoReport = promoReport
            };

            return Results.Ok(report);
        }

        [HttpPost]
        public async Task<IActionResult> PrincessLogout()
        {
            _staffTMS.CloseTerminal();
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> WriteTicket(string? cartId = null)
        {
            _context.ProductCategories
                .Include(pc => pc.Ingredients)
                .Include(pc => pc.MenuItems)
                .Load();
            _context.MenuItems
                .Include(mi => mi.Ingredients)
                .Include(mi => mi.ProductCategory)
                .Include(mi => mi.RawDbVarients)
                .Load();
            _context.MenuItemVarients.Load();
            _context.Ingredients
                .Include(i => i.MenuItems)
                .Load();

            var menu = _context.MenuItems
                .Include(mi => mi.Ingredients)
                .ToList();

            foreach (var item in menu)
            {
                item.RawDbVarients = await _context.MenuItemVarients
                    .Where(miv => miv.MenuItemId == item.Id)
                    .OrderBy(mi => mi.Id)
                    .ToListAsync();
            }

            cartId = cartId ?? _cartService.ProvisionCart();
            var cart = _cartService.GetCart(cartId) ?? throw new ArgumentNullException();

            return View(new WriteTicketVM
            {
                CartId = cartId,
                Cart = cart,
                CustomerName = cart.Customer?.Name ?? string.Empty,
                CustomerPhone = cart.Customer?.Phone ?? string.Empty,
                PromoCode = cart.PromoCode ?? string.Empty,
                Menu = menu,
                AvailablePromoCodes = _context.DiscountCodes.Where(c => !c.IsArchived).ToList()
            });
        }

        [HttpPost]
        public async Task<IActionResult> WriteTicket(WriteTicketVM model)
        {
            if (!ModelState.IsValid)
            {
                await _context.Ingredients.LoadAsync();
                await _context.ProductCategories.LoadAsync();

                model.Menu = await _context.MenuItems
                    .Include(mi => mi.RawDbVarients)
                    .Include(mi => mi.Ingredients)
                    .ToListAsync();

                model.Cart = _cartService.GetCart(model.CartId);

                return View(model);
            }

            var customerDetailModel = new CartCustomer
            {
                CartId = model.CartId,
                Name = model.CustomerName,
                Phone = model.CustomerPhone,
                Email = "null@void.example",
                EmailUpdates = false,
                SmsUpdates = false
            };

            var promoCodeModel = new ApplyCouponModel
            {
                CartId = model.CartId,
                PromoCode = model.PromoCode ?? ""
            };

            using (var client = new HttpClient())
            {
                var host = HttpContext.Request.Host.ToString();
                client.BaseAddress = new Uri($"https://{host}");

                await client.PutAsync("/API/Cart/ApplyDiscount", JsonContent.Create(promoCodeModel));

                var response = await client.PutAsync("/API/Cart/Customer", JsonContent.Create(customerDetailModel));

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, response.Content);

                response = await client.PostAsync($"/API/Cart/Submit?cartId={model.CartId}", JsonContent.Create(new { dumdum = "some data" }));

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, response.Content);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddItemToTicket(string cartId, int itemId, int varient)
        {
            var addItemModel = new CartAddItemModel
            {
                CartId = cartId,
                ItemId = itemId,
                Varient = varient
            };

            var client = new HttpClient();
            var host = HttpContext.Request.Host.ToString();
            client.BaseAddress = new Uri($"https://{host}");
            var response = await client.PutAsync("/API/Cart/AddItem", JsonContent.Create(addItemModel));

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, response.Content);

            return RedirectToAction(nameof(WriteTicket), new { cartId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveItemFromTicket(string cartId, int position)
        {
            var removeItemModel = new CartUpdateItemModel
            {
                CartId = cartId,
                Index = position
            };

            using (var client = new HttpClient())
            {
                var host = HttpContext.Request.Host.ToString();
                client.BaseAddress = new Uri($"https://{host}");

                var response = await client.PutAsync("/API/Cart/RemoveItem", JsonContent.Create(removeItemModel));

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, response.Content);
            }

            return RedirectToAction(nameof(WriteTicket), new { cartId });
        }

        [HttpPost]
        public async Task<IActionResult> ModifyTicketItem(string cartId, int position, int varient, string comments, int[] ingredients)
        {
            var updateItemModel = new CartUpdateItemModel
            {
                CartId = cartId,
                Index = position,
                IngredientIds = ingredients,
                VarientIndex = varient,
                LineComments = comments
            };

            using (var client = new HttpClient())
            {
                var host = HttpContext.Request.Host.ToString();
                client.BaseAddress = new Uri($"https://{host}");

                var response = await client.PutAsync("/API/Cart/UpdateItem", JsonContent.Create(updateItemModel));

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, response.Content);
            }

            return RedirectToAction(nameof(WriteTicket), new { cartId });
        }

    }
}
