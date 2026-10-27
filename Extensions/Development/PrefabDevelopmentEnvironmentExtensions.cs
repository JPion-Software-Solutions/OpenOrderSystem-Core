using System;
using Microsoft.AspNetCore.Identity;
using Microsoft.Identity.Client;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.DevelopmentTools;
using Quartz.Xml.JobSchedulingData20;

namespace OpenOrderSystem.Core.Extensions.Development;

public static class PrefabDevelopmentEnvironmentExtensions
{
    /// <summary>
    /// Determines, before the host is built, whether the prefab development standup will run:
    /// the environment is Development and <c>OOS_Dev_Env:Configuration</c> is not <see cref="PrefabConfiguration.None"/>.
    /// </summary>
    /// <remarks>
    /// Used by boot preflight, which runs before <see cref="WebApplication"/> configuration exists, so the
    /// same sources are read directly (appsettings, user secrets, environment variables).
    /// </remarks>
    public static bool IsPrefabStandupEnabled()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? Environments.Production;

        if (!string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase))
            return false;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddUserSecrets(typeof(PrefabDevelopmentEnvironmentExtensions).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var devEnv = configuration.GetSection("OOS_Dev_Env").Get<PrefabDevelopmentSettings>();
        return devEnv is not null && devEnv.Configuration != PrefabConfiguration.None;
    }

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

        // Identity lives in the V2 database (OosDbContext), so users are created through the
        // UserManager rather than the legacy ApplicationDbContext.
        if (userManger.Users.Any())
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

            await CreateDevUser(userManger, adminCred, ["admin","default_admin","manager","terminal_user"]);
            await CreateDevUser(userManger, userCred, ["terminal_user"]);

            logger.LogInformation("User accounts created successfully!");
        }

        try
        {
            await ImportDevPack(devEnv, httpClient, dbContext);
        }
        catch (Exception ex)
        {
            // A dev pack outage (e.g. an expired certificate on the asset store) shouldn't block booting.
            logger.LogWarning(ex, "Unable to import dev pack '{DevPackName}' from '{DevPackRepoUrl}'. Continuing without it.",
                devEnv.DevPackName, devEnv.DevPackRepoUrl);
        }
    }

    /// <summary>
    /// Creates a development user with the given credentials and roles. The password is hashed directly
    /// so the simple dev defaults bypass the production password policy.
    /// </summary>
    private static async Task CreateDevUser(UserManager<IdentityUser> userManager, AccountCredentials credentials, string[] roles)
    {
        var user = new IdentityUser { UserName = credentials.username };
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, credentials.password);

        var result = await userManager.CreateAsync(user);
        if (result.Succeeded)
            result = await userManager.AddToRolesAsync(user, roles);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Unable to create dev user '{credentials.username}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    private static async Task ImportDevPack(PrefabDevelopmentSettings devEnv, HttpClient httpClient, ApplicationDbContext dbContext)
    {
        var manifestClient = new DevPackManifestClient(httpClient);
        var manifest = await manifestClient.GetManifestAsync($"{devEnv.DevPackRepoUrl}/manifest.json");
        var pack = manifest.Packs.FirstOrDefault(p => p.PackName == devEnv.DevPackName);
        if (pack ==  null) throw new InvalidOperationException($"Unable to locate pack '{devEnv.DevPackName}' at '{devEnv.DevPackRepoUrl}'");

        var filename = pack.Versions[pack.Latest].Filename;
        var filepath = $"{devEnv.DevPackRepoUrl}/{filename}";
        
        Directory.CreateDirectory(Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store","DevPacks"));

        if (!File.Exists(Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store","DevPacks",filename)))
        {
            var downloadName = await ZipDownloader.DownloadZipToDiskAsync(httpClient, filepath, Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store", "DevPacks"), filename, pack.Versions[pack.Latest].Sha256);
            filepath = Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store","DevPacks",filename);

            //unpack environment.
            await DevPackUnloader.UnpackAssets(filepath);
            await DevPackUnloader.ImportInfo(filepath, dbContext);
            await DevPackUnloader.ImportMenu(filepath, dbContext);
        }
    }

    public class PrefabDevelopmentSettings
    {
        private static readonly string DEFAULT_REPO_LOCATION = "https://api.openordersystem.cloud/Store/DevPack";
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
