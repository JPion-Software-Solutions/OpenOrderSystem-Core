using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Extensions.AspNet;
using OpenOrderSystem.Core.Utilities;

namespace OpenOrderSystem.Core.Middleware.__WebApplicationExtensions;

public static class DatabaseAppExtensions
{
    /// <summary>
    /// Brings every OOS database up to date, seeds the core user roles, and (in development) maps
    /// the EF migrations endpoint. Call it once, right after <c>Build()</c>.
    /// </summary>
    /// <param name="app">The built web application.</param>
    /// <param name="config">The OOS boot configuration.</param>
    public static WebApplication MigrateOosDatabases(this WebApplication app, Configuration config)
    {
        using (var scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<OosDbContext>().Database.Migrate();

            var legacyComponents = config.GetConfig<string[]>("LEGACY_COMPONENTS");

            if (legacyComponents != null && legacyComponents.Contains("DATABASE"))
            {
                // The legacy context only has SQL Server migrations (they use SQL Server column types such
                // as nvarchar(max)). On any other provider, build its schema straight from the model instead.
                var legacy = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (config.GetConfig<DbProviders>("LEGACY:DB_PROVIDER") == DbProviders.SQLServer)
                    legacy.Database.Migrate();
                else
                    legacy.Database.EnsureCreated();
            }
        }

        // Roles live in the Identity tables, so they can only be seeded once migrations have run.
        app.EnsureCoreUserRolesCreated().Wait();

        if (app.Environment.IsDevelopment())
            app.UseMigrationsEndPoint();

        return app;
    }
}