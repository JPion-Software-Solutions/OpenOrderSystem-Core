using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{
    [Area("API")]
    [ApiController]
    [Route("API/Customer/{action}")]
    public class CustomerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CustomerController> _logger;
        private readonly ConfigurationService _config;

        public CustomerController(ApplicationDbContext context, ILogger<CustomerController> logger, ConfigurationService config)
        {
            _context = context;
            _logger = logger;
            _config = config;

            _context.DiscountCodes
                .Include(d => d.WhiteListItemsVarients)
                .Include(d => d.Orders)
                .Load();
        }

        [HttpGet]
        public IResult Anonymise()
        {
            var customersPurged = 0;
            var customers = _context.Customers
                .Where(c => c.CustomerCreated.AddHours(_config.Settings.CustomerDataRetentionPolicy) < DateTime.UtcNow &&
                    c.Name != c.Phone && c.Phone != c.Email)
                .ToList();

            foreach (var customer in customers)
            {
                //unlink orders
                var orders = _context.Orders
                    .Where(o => o.CustomerId == customer.Id)
                    .ToList();
                foreach (var order in orders)
                {
                    order.CustomerId = null;
                    _context.Orders.Update(order); //ensures customer info is completely detached from order(s) placed
                }

                //anonymise customer details
                customer.Name = customer.Email = customer.Phone = "[Information Redacted]";
                customer.SMSUpdates = customer.EmailUpdates = false;

                _context.Update(customer);
                ++customersPurged;
            }

            _context.SaveChanges();

            if (customersPurged == 0)
                return Results.NoContent();

            return Results.Ok(new
            {
                message = $"Successfully purged {customersPurged} expired customers from the database."
            });
        }
    }
}
