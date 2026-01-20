using System;
using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Data;

namespace OpenOrderSystem.Core.Extensions.Development;

public static class PrefabDevelopmentEnvironmentExtensions
{
    public static void EnsureDevEnvironmentReady(this WebApplication app)
    {
        var section = app.Configuration.GetSection("OOS_Dev_Env");

        PrefabDevelopmentSettings? devEnv =
            section.Exists()
                ? section.Get<PrefabDevelopmentSettings>()
                : null;
        
        if (devEnv == null || devEnv.Configuration == PrefabConfiguration.None) return;

        if (!app.Environment.IsDevelopment())
            throw new InvalidOperationException("Development seed data is only available when running in development mode!");
        
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();


        logger.LogInformation("DEVELOPMENTD DATABASE ACTIVE");

        if (dbContext.Users.Any())
        {
            logger.LogError("INVALID OPERATION! ");
        }
    }

    public class PrefabDevelopmentSettings
    {
        private static readonly string DEFAULT_REPO_LOCATION = "https://api.jpion.codes/OOS-Asset-Store/DevPacks";
        private static readonly string DEFAULT_DEV_PACK_NAME = "default-pack";

        public PrefabConfiguration Configuration { get; set; } = PrefabConfiguration.None;

        /// <summary>
        /// Determines the user credentials that will be used to create the admin account. Use format "username/password"
        /// </summary>
        public string AdminUserCredentials { get; set; } = "admin/password";


        /// <summary>
        /// Determines the user credentials that will be used to create the standard user account. Use format "username/password"
        /// </summary>
        public string StandardUserCredentials { get; set; } = "user/password";

        /// <summary>
        /// Location of the development pack repository where the dev pack is stored.
        /// </summary>
        public string DevPackRepoUrl { get; set; } = DEFAULT_REPO_LOCATION;

        /// <summary>
        /// Name of the default dev pack to download.
        /// </summary>
        public string DevPackName { get; set; } = DEFAULT_DEV_PACK_NAME;
    }
}

public enum PrefabConfiguration
{
    None,

    __FoodServiceStart = 100,

    FoodServiceGeneric,

    __FoodServiceEnd = 199
}
