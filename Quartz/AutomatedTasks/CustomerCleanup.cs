using OpenOrderSystem.Core.Data;
using Quartz;

namespace OpenOrderSystem.Core.Quartz.AutomatedTasks
{
    public class CustomerCleanup : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<CustomerCleanup> _logger;

        public CustomerCleanup(IServiceScopeFactory serviceScopeFactory, ILogger<CustomerCleanup> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var scope = _serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var expiredCustomers = dbContext.Customers
                .Where(c => c.CustomerCreated.AddHours(12) < DateTime.UtcNow)
                .ToList();

            _logger.LogInformation("Started customer privacy clean-up.");

            if (expiredCustomers.Any())
            {
                _logger.LogInformation($"Removing {expiredCustomers.Count} expired customers.");

                foreach (var customer in expiredCustomers)
                {
                    var orders = dbContext.Orders
                        .Where(o => o.CustomerId == customer.Id)
                        .ToList();

                    foreach (var order in orders)
                    {
                        order.CustomerId = null;
                    }

                    customer.Name = customer.Phone = customer.Email = "[ REDACTED ]";
                }

                var rows = await dbContext.SaveChangesAsync();

                _logger.LogInformation($"Customer clean-up complete. {rows} rows effected");
            }
            else
            {
                _logger.LogInformation("No expired customers found.");
            }
        }
    }
}
