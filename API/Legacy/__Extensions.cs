using Microsoft.AspNetCore.RateLimiting;
using OpenOrderSystem.Core.API.Legacy.Cart;
using OpenOrderSystem.Core.API.Legacy.Customers;
using OpenOrderSystem.Core.API.Legacy.Identity;
using OpenOrderSystem.Core.API.Legacy.Menu;
using OpenOrderSystem.Core.API.Legacy.Orders;
using OpenOrderSystem.Core.API.Legacy.Print;
using OpenOrderSystem.Core.API.Legacy.Staff;
using OpenOrderSystem.Core.API.Legacy.System;
using OpenOrderSystem.Core.API.V2.System;
using OpenOrderSystem.Core.Attributes;

namespace OpenOrderSystem.Core.API.Legacy;

public static class __Extensions
{
    /// <summary>
    /// Maps the legacy API. Its shape is locked: every route, method, input and output matches the MVC controllers
    /// it replaced.
    /// </summary>
    /// <remarks>
    /// The old controllers were <c>[ApiController]</c> except OrderController. Those groups get
    /// <c>DisableCookieRedirect</c> so an unauthenticated request still gets a 401 rather than a login redirect;
    /// the Order group keeps the redirect.
    /// </remarks>
    public static WebApplication MapLegacyApiRoutes(this WebApplication app)
    {
        // System. These accepted any HTTP method.
        app.Map("/api/system/ping", PingUtility.Get).DisableCookieRedirect();
        app.Map("/ping", PingUtility.Get).DisableCookieRedirect();

        var system = app.MapGroup("/api/legacy/System").DisableCookieRedirect();
        system.Map("/Ping", PingUtility.Get);
        system.Map("/AcceptingOrders", SystemApi.AcceptingOrders);

        // Menu. These accepted any HTTP method.
        var menu = app.MapGroup("/api/legacy/Menu").DisableCookieRedirect();
        menu.Map("/GetMenuItems", MenuApi.GetMenuItems);
        menu.Map("/GetItemDetail", MenuApi.GetItemDetail);

        // Identity
        var identity = app.MapGroup("/api/legacy/Identity").DisableCookieRedirect();
        identity.MapPost("/Login", IdentityApi.Login);
        identity.MapPost("/Logout", IdentityApi.Logout).RequireAuthorization();
        identity.MapGet("/TestAuth", IdentityApi.TestAuth).RequireAuthorization();

        // Customer
        var customer = app.MapGroup("/api/legacy/Customer").DisableCookieRedirect();
        customer.MapGet("/Anonymise", CustomerApi.Anonymise);

        // Cart
        var cart = app.MapGroup("/api/legacy/Cart").DisableCookieRedirect();
        cart.MapPut("/AddItem", CartApi.AddItem);
        cart.MapGet("/ItemDetail/{cartId}/{index}", CartApi.ItemDetail);
        cart.MapPut("/RemoveItem", CartApi.RemoveItem);
        cart.MapPost("/New", CartApi.New);
        cart.MapGet("/Details", CartApi.Details);
        cart.MapPost("/Submit", CartApi.Submit);
        cart.MapPut("/ApplyDiscount", CartApi.ApplyDiscount);
        cart.MapPut("/Customer", CartApi.Customer);
        cart.MapPut("/UpdateItem", CartApi.UpdateItem);
        cart.MapDelete("/Dispose", CartApi.Dispose);

        // Order (a plain controller: login redirects stay on)
        app.MapGet("/api/legacy/checkOrder/{orderId}", OrderApi.CheckStatus).AllowCookieRedirect();

        var order = app.MapGroup("/api/legacy/Order").AllowCookieRedirect();
        order.MapGet("/Locate/{method}/{key}", OrderApi.Locate);
        order.MapGet("/IsOpen", OrderApi.IsOpen);
        order.MapGet("/Detail/Staff/Orders/Detail/{id}", OrderApi.Detail).RequireAuthorization();
        order.MapPut("/UpdateStatus", OrderApi.UpdateStatus).RequireAuthorization();
        order.MapPut("/TerminalCheckin", OrderApi.TerminalCheckin).RequireAuthorization();
        order.MapPut("/TerminalClose", OrderApi.TerminalClose).RequireAuthorization();
        order.MapDelete("/CancelOrder", OrderApi.CancelOrder).RequireAuthorization();

        // Print (authorized unless noted)
        var print = app.MapGroup("/api/legacy/Print").DisableCookieRedirect().RequireAuthorization();
        print.MapGet("/Ping", PrintApi.Ping).AllowAnonymous();
        print.MapPost("/Register", PrintApi.Register);
        print.MapPut("/Register", PrintApi.RegisterExisting).AllowAnonymous();
        print.MapPut("/CheckIn", PrintApi.CheckIn).AllowAnonymous()
            .WithMetadata(new ValidatePrintBridgeAttribute())
            .DisableRateLimiting();
        print.MapPut("/CompleteJob", PrintApi.CompleteJob).AllowAnonymous()
            .WithMetadata(new ValidatePrintBridgeAttribute());
        print.MapPost("/PrintOrderTicket", PrintApi.PrintOrderTicket);
        print.MapPost("/TestPrint", PrintApi.TestPrint);

        // Staff terminal service (all authorized)
        var terminal = app.MapGroup("/api/legacy/Staff/TerminalService").DisableCookieRedirect().RequireAuthorization();
        terminal.MapGet("/Alerts", TerminalServiceApi.Alerts);
        terminal.MapPut("/AddOrderTime", TerminalServiceApi.AddOrderTime);
        terminal.MapPut("/Ping", TerminalServiceApi.Ping).DisableRateLimiting();
        terminal.MapGet("/FetchPrinterStatus", TerminalServiceApi.FetchPrinterStatus);
        terminal.MapGet("/FetchOrders", TerminalServiceApi.FetchOrders);

        return app;
    }
}
