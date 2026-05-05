using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Utilities;

namespace OpenOrderSystem.Core.Data.DataModels.V2;

public class OosDbContextFactory : IDesignTimeDbContextFactory<OosDbContext>
{
    public OosDbContext CreateDbContext(string[] args)
    {
        var config = Configuration.Load().GetAwaiter().GetResult();
        var optionsBuilder = new DbContextOptionsBuilder<OosDbContext>();
        optionsBuilder.UseDynamicSQLProvider(config);
        return new OosDbContext(optionsBuilder.Options);
    }
}
