using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.API.Legacy.Menu;

/// <summary>Legacy <c>api/legacy/Menu</c> endpoints (formerly MenuController). Both accept any HTTP method.</summary>
public static class MenuApi
{
    public static IResult GetMenuItems([FromServices] ApplicationDbContext context)
    {
        var menu = context.MenuItems
            .Include(mi => mi.RawDbVarients)
            .Include(mi => mi.ProductCategory)
            .Where(mi => mi.Priority >= 0 && mi.ProductCategory != null && mi.ProductCategory.Priority >= 0)
            .OrderByDescending(mi => mi.ProductCategory!.Priority)
            .ThenByDescending(mi => mi.Priority)
            .ToList();

        foreach (var menuItem in menu)
        {
            menuItem.ProductCategory!.MenuItems = null;

            foreach (var variant in menuItem.RawDbVarients ?? new List<MenuItemVarient>())
            {
                variant.MenuItem = null;
            }
        }

        return Results.Ok(menu);
    }

    public static IResult GetItemDetail(HttpContext httpContext)
    {
        var binder = new MvcCompatBinder(httpContext);
        binder.Query<int>("id");
        if (binder.ApiControllerError() is { } error) return error;

        return Results.StatusCode(418);
    }
}
