using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.DevelopmentTools;
using Quartz.Xml.JobSchedulingData20;

namespace OpenOrderSystem.Core.Extensions.Development;

public static class PrefabDevelopmentEnvironmentExtensions
{
    public static async Task EnsureDevEnvironmentReady(this WebApplication app)
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
        var userManger = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();
        var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

        logger.LogInformation("DEVELOPMENTD DATABASE ACTIVE");

        if (dbContext.Users.Any())
        {
            logger.LogError("INVALID OPERATION! Cannot add users to a system already containing users.");
        }
        else
        {
            var adminCred = devEnv.AdminUserCredentials.Split('/').Length == 2
                ? new AccountCredentials(devEnv.AdminUserCredentials.Split('/')[0], devEnv.AdminUserCredentials.Split('/')[1])
                : new AccountCredentials("admin", "password"); //fallback;
            
            var userCred = devEnv.StandardUserCredentials.Split('/').Length == 2
                ? new AccountCredentials(devEnv.StandardUserCredentials.Split('/')[0], devEnv.StandardUserCredentials.Split('/')[1])
                : new AccountCredentials("user", "password"); //fallback;

            var hasher = new PasswordHasher<IdentityUser>();

            var admin = new IdentityUser
            {
                UserName = adminCred.username,
                NormalizedUserName = adminCred.username.ToUpper()
            };
            var user = new IdentityUser
            {
                UserName = userCred.username,
                NormalizedUserName = userCred.username.ToUpper()
            };

            admin.PasswordHash = hasher.HashPassword(admin, adminCred.password);
            user.PasswordHash = hasher.HashPassword(user, userCred.password);

            dbContext.AddRange([admin, user]);
            await dbContext.SaveChangesAsync();

            await userManger.AddToRolesAsync(admin, ["admin","default_admin","manager","terminal_user"]);
            await userManger.AddToRolesAsync(user, ["terminal_user"]);

            logger.LogInformation("User accounts created successfully!");
        }

        var manifestClient = new DevPackManifestClient(httpClient);
        var manifest = await manifestClient.GetManifestAsync($"{devEnv.DevPackRepoUrl}/manifest.json");
        var pack = manifest.Packs.FirstOrDefault(p => p.PackName == devEnv.DevPackName);
        if (pack ==  null) throw new InvalidOperationException($"Unable to locate pack '{devEnv.DevPackName}' at '{devEnv.DevPackRepoUrl}'");

        var filename = pack.Versions[pack.Latest].Filename;
        var filepath = $"{devEnv.DevPackRepoUrl}/{filename}";

        Directory.CreateDirectory(Path.Combine("Store","DevPacks"));

        if (!File.Exists(Path.Combine("Store","DevPacks",filename)))
        {
            var downloadName = await ZipDownloader.DownloadZipToDiskAsync(httpClient, filepath, "Store/DevPacks", filename, pack.Versions[pack.Latest].Sha256);
            filepath = Path.Combine("Store","DevPacks",filename);

            //unpack environment.
            await DevPackUnloader.UnpackAssets(filepath);
            await DevPackUnloader.ImportInfo(filepath, dbContext);
            await DevPackUnloader.ImportMenu(filepath, dbContext);
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

    public record AccountCredentials (string username, string password);
}

public enum PrefabConfiguration
{
    None,

    __FoodServiceStart = 100,

    FoodServiceGeneric,

    __FoodServiceEnd = 199
}
