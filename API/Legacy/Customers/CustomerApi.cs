using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services.Legacy;

namespace OpenOrderSystem.Core.API.Legacy.Customers;

/// <summary>Legacy <c>api/legacy/Customer</c> endpoints (formerly CustomerController).</summary>
public static class CustomerApi
{
    // GET
    public static IResult Anonymise([FromServices] ApplicationDbContext context, [FromServices] ConfigurationService config)
    {
        // The old controller preloaded discount codes into the change tracker on construction.
        context.DiscountCodes
            .Include(d => d.WhiteListItemsVarients)
            .Include(d => d.Orders)
            .Load();

        var customersPurged = 0;
        var customers = context.Customers
            .Where(c => c.CustomerCreated.AddHours(config.Settings.CustomerDataRetentionPolicy) < DateTime.UtcNow &&
                c.Name != c.Phone && c.Phone != c.Email)
            .ToList();

        foreach (var customer in customers)
        {
            //unlink orders
            var orders = context.Orders
                .Where(o => o.CustomerId == customer.Id)
                .ToList();
            foreach (var order in orders)
            {
                order.CustomerId = null;
                context.Orders.Update(order); //ensures customer info is completely detached from order(s) placed
            }

            //anonymise customer details
            customer.Name = customer.Email = customer.Phone = "[Information Redacted]";
            customer.SMSUpdates = customer.EmailUpdates = false;

            context.Update(customer);
            ++customersPurged;
        }

        context.SaveChanges();

        if (customersPurged == 0)
            return Results.NoContent();

        return Results.Ok(new
        {
            message = $"Successfully purged {customersPurged} expired customers from the database."
        });
    }
}
