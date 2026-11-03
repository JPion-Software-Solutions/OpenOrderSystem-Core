using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.Providers;
using OpenOrderSystem.Core.Extensions.AspNet;
using OpenOrderSystem.Core.Utilities;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

public static class DatabaseBuilderExtensions
{
    /// <summary>
    /// Registers the OOS database contexts (OosDbContext, DataProtectionKeyContext, and the legacy
    /// ApplicationDbContext) and stores Data Protection keys in the database.
    /// </summary>
    /// <param name="bob">The web application builder.</param>
    /// <param name="config">The OOS boot configuration. It holds DB_PROVIDER / DB_CONNECTION_STRING and
    /// the LEGACY: equivalents. This is not <c>bob.Configuration</c>.</param>
    public static WebApplicationBuilder AddOosDatabaseServices(this WebApplicationBuilder bob, Configuration config)
    {
        if (bob.Environment.IsDevelopment())
            bob.Services.AddDatabaseDeveloperPageExceptionFilter();

        // OosDbContext and DataProtectionKeyContext are backed by provider-specific subclasses,
        // each with its own migration set (see Data/Providers).
        bob.Services.AddOosDbContexts(config);

        bob.Services.AddDataProtection()
            .PersistKeysToDbContext<DataProtectionKeyContext>()
            .SetApplicationName("OpenOrderSystem");
        
        var legacyComponents = config.GetConfig<string[]>("LEGACY_COMPONENTS");

        if (legacyComponents != null && legacyComponents.Contains("DATABASE"))
        {        
            bob.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseDynamicSQLProvider(config, "LEGACY");
                options.EnableDetailedErrors();
            });
        }
        
        return bob;
    }
}
