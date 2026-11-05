using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;
using OpenOrderSystem.Core.Services.Legacy;
using OpenOrderSystem.Core.Services.Sms.Interfaces;
using PizzaPartry.tools;
using static OpenOrderSystem.Core.Areas.API.Legacy.Controllers.OrderController;

namespace OpenOrderSystem.Core.API.Legacy.Orders;

/// <summary>
/// Legacy <c>api/legacy/Order</c> endpoints (formerly OrderController). Unlike the rest of the legacy API this was a
/// plain MVC controller, not an <c>[ApiController]</c>: failed binding is not rejected (values fall back to their
/// defaults), <c>NotFound("...")</c> is content-negotiated, and unauthenticated requests are redirected to login.
/// </summary>
public static class OrderApi
{
    // GET {method}/{key}
    public static async Task<IResult> Locate(HttpContext httpContext, [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var key = binder.RouteString("key", required: false)!;
        var method = binder.Route("method", LocateMethod.OrderId);

        var orders = Array.Empty<int>();
        if (method == LocateMethod.OrderId)
        {
            if (int.TryParse(key, out var parsedKey))
            {
                var temp = await context.Orders
                    .Where(o => o.Id == parsedKey)
                    .ToListAsync();

                orders = temp
                    .Select(o => o.Id)
                    .ToArray();
            }
        }
        else
        {

            var temp = await context.Orders
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

    // GET /api/legacy/checkOrder/{orderId}
    public static IResult CheckStatus(HttpContext httpContext, [FromServices] ApplicationDbContext context)
    {
        var orderId = new MvcCompatBinder(httpContext).Route<int>("orderId");

        // First fetch: simple existence + timestamps
        var order = context.Orders.FirstOrDefault(o => o.Id == orderId);

        if (order == null)
        {
            return MvcCompatResults.Negotiated($"failed to locate order#:{orderId}", StatusCodes.Status404NotFound);
        }

        // Second fetch: light detail load for the status page
        var orderDetails = context.Orders
            .Include(o => o.Customer)
            .Include(o => o.Discount)
            .Include(o => o.LineItems)
                .ThenInclude(li => li.MenuItem)
                    .ThenInclude(mi => mi!.RawDbVarients)
            .FirstOrDefault(o => o.Id == orderId);

        context.OrderLines
            .Include(ol => ol.Ingredients)
            .Include(ol => ol.MenuItem)
                .ThenInclude(mi => mi.Ingredients)
            .Load();

        // Build a light detail object (not the massive staff version)
        var details = new
        {
            linetotal = orderDetails?.LineItemTotal.ToString("C") ?? "",
            subtotal = orderDetails?.Subtotal.ToString("C") ?? "",
            tax = orderDetails?.Tax.ToString("C") ?? "",
            total = orderDetails?.Total.ToString("C") ?? "",
            promoCode = orderDetails?.DiscountId,
            discout = (orderDetails?.Discount?.GetDiscount(orderDetails, true) ?? 0.0f).ToString("C"),
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

        return Results.Json(new
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

    // GET
    public static async Task<IResult> IsOpen(
        [FromServices] StaffTerminalMonitoringService staffTMS,
        [FromServices] ConfigurationService config,
        [FromServices] ITimeNormalizationService timeService)
    {
        var currentLocalTime = await timeService.GetLocalTime();

        return MvcCompatResults.Negotiated(
            staffTMS.TerminalActive &&                                    //verifys the staff terminal hasn't lost connection
            config.Settings.IsAcceptingOrders(currentLocalTime));         //verifys time within scheduled ordering hours
    }

    // GET Detail/Staff/Orders/Detail/{id}, authorized
    public static IResult Detail(HttpContext httpContext, [FromServices] ApplicationDbContext context)
    {
        var id = new MvcCompatBinder(httpContext).Route<int>("id");

        var order = context.Orders
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

    // PUT, authorized
    public static async Task<IResult> UpdateStatus(
        HttpContext httpContext,
        [FromServices] ApplicationDbContext context,
        [FromServices] IEmailService emailService,
        [FromServices] ISmsService smsService)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = await binder.BodyAsync<UpdateStatusModel>("model");
        if (binder.UnsupportedMediaTypeError() is { } error) return error;

        // A missing or unreadable body leaves model null and throws here, as it always has.
        var orderId = model!.OrderId;

        var order = context.Orders
            .Include(o => o.Customer)
            .FirstOrDefault(o => o.Id == orderId);

        if (order == null)
        {
            return MvcCompatResults.Negotiated($"failed to locate order#:{orderId}", StatusCodes.Status404NotFound);
        }

        if (order.StageLegacy == OrderStageLegacy.Ready)
        {
            order = context.Orders
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
                return MvcCompatResults.Negotiated($"failed to locate order#:{orderId}", StatusCodes.Status404NotFound);
            }
        }

        order.CompleteStage();
        await context.SaveChangesAsync();

        if (order.Customer != null)
            //TODO Replace direct email service usage with abstracted CustomerNotificationService
            await AlertCustomer(emailService, smsService, order.Customer, order.StageLegacy);

        return Results.Json(new
        {
            message = $"Order#{order.Id} advanced to stage {order.StageLegacy}."
        });
    }

    // PUT, authorized
    public static void TerminalCheckin(
        [FromServices] ApplicationDbContext context,
        [FromServices] StaffTerminalMonitoringService staffTMS,
        [FromServices] IEmailService emailService,
        [FromServices] ISmsService smsService)
    {
        staffTMS.RegisterCheckin();
        var ordersInProgress = context.Orders
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
                context.Update(order);
                context.SaveChanges();

                // Not awaited, as before.
                if (order.Customer != null)
                    _ = AlertCustomer(emailService, smsService, order.Customer, order.StageLegacy);
            }
            if (order.CheckTimer() == TimerStatus.LessThanTwo)
            {
                staffTMS.TriggerOrderTimerAlert();
            }
        }
    }

    // PUT, authorized
    public static void TerminalClose([FromServices] StaffTerminalMonitoringService staffTMS) => staffTMS.CloseTerminal();

    // DELETE, authorized
    public static async Task<IResult> CancelOrder(HttpContext httpContext, [FromServices] ApplicationDbContext context)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = await binder.BodyAsync<UpdateStatusModel>("model");
        if (binder.UnsupportedMediaTypeError() is { } error) return error;

        int orderNumber = model!.OrderId;
        var order = context.Orders.FirstOrDefault(o => o.Id == orderNumber);
        if (order == null)
            return Results.NotFound(new
            {
                OrderId = orderNumber,
                Message = $"Unable to locate order #{orderNumber}."
            });

        context.Orders.Remove(order);
        context.SaveChanges();

        return Results.Ok(new
        {
            Message = $"Order #{orderNumber} successsfully canceled"
        });
    }

    private static async Task AlertCustomer(IEmailService emailService, ISmsService smsService,
        Customer customer, OrderStageLegacy stageLegacy)
    {
        switch (stageLegacy)
        {
            case OrderStageLegacy.Ready:
                if (customer.EmailUpdates)
                {
                    await emailService.SendAsync(
                        customer.Email,
                        "Village Market Pizza Order",
                        "Your order is ready for pickup. Thank you for ordering from Village Market.");
                }

                if (customer.SMSUpdates)
                {
                    var phone = smsService.ConvertPhone(customer.Phone);
                    smsService.SendSMS(phone, "Your order is ready for pickup. Thank " +
                        "you for ordering from Village Market Rapid City.");
                }
                break;
        }
    }
}
