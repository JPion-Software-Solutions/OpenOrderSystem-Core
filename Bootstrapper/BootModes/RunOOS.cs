using System;
using System.ComponentModel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenOrderSystem.Core.API.Legacy;
using OpenOrderSystem.Core.API.V2;
using OpenOrderSystem.Core.API.V2.System;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.Providers;
using OpenOrderSystem.Core.Extensions.AspNet;
using OpenOrderSystem.Core.Extensions.Development;
using OpenOrderSystem.Core.Middleware;
using OpenOrderSystem.Core.Middleware.__WebApplicationExtensions;
using OpenOrderSystem.Core.Quartz.AutomatedTasks;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.Config;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Legacy;
using OpenOrderSystem.Core.Services.Sms;
using OpenOrderSystem.Core.Services.Sms.Interfaces;
using OpenOrderSystem.Core.Services.EmailService;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;
using OpenOrderSystem.Core.Services.Keyring;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;
using OpenOrderSystem.Core.Services.UI.Nav.Admin;
using OpenOrderSystem.Core.Services.UI.Nav.Admin.Interface;
using OpenOrderSystem.Core.Services.UI.Theme.Icons.BuiltinPacks;
using OpenOrderSystem.Core.Services.UI.Theme.Icons.Interface;
using OpenOrderSystem.Core.Utilities;
using Quartz;
using Serilog;
using SixLabors.ImageSharp.Processing;

namespace OpenOrderSystem.Core.Bootstrapper.BootModes;

[BootModeUi(DisplayName = "Run Open Order System" ,Description ="Run Open Order System (normal operation).")]
public class RunOosBootstrap : IBootMode
{
    private WebApplicationBuilder? _bob;
    private WebApplication? _app;

    private readonly string _corsPolicy = "OOS_CORS_ACCESS_POLICY";

    private string? _fallback = null;
    private KeyringBuildResult? _keyringBuildResult;

    public string? RequestedFallback => _fallback;

    public WebApplicationBuilder? Bob => _bob;
    
    public WebApplication? App => _app;
    
    public bool IsDevelopment { get; private set; }

    public Configuration? Configuration { get; private set; }

    public WebApplication ConfigureMiddleware()
    {
        _app = _bob?.Build()
            ?? throw new InvalidOperationException("Unable to build OOS Server, missing builder! Please ensure Initialize is called before attempting to build.");

        using (var scope = _app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<OpenOrderSystemApplication>>();

            //log any keyring build actions taken.
            var actions = _keyringBuildResult?.Actions ?? KeyringBootActions.None;

            if (actions.HasFlag(KeyringBootActions.Created))
                logger.LogInformation("Keyring: no keyring was found, so a new one was created.");
            if (actions.HasFlag(KeyringBootActions.EncryptionEnabled))
                logger.LogInformation("Keyring: existing pass-through secrets were encrypted under a new data key.");
            if (actions.HasFlag(KeyringBootActions.MovedToEnvironment))
                logger.LogInformation("Keyring: the data key is now protected by OOS_MASTERKEY.");
            if (actions.HasFlag(KeyringBootActions.EnvironmentKeyReplaced))
                logger.LogWarning("Keyring: unlocked with OOS_MASTERKEY_PREVIOUS and re-protected with OOS_MASTERKEY. " +
                                  "Remove OOS_MASTERKEY_PREVIOUS from the environment.");
            if (actions.HasFlag(KeyringBootActions.KdfUpgraded))
                logger.LogInformation("Keyring: the master key derivation was upgraded to stronger settings.");
            if (actions.HasFlag(KeyringBootActions.ReleasedToDisk))
                logger.LogWarning("Keyring: OOS_MASTERKEY_RELEASE is set, so the data key was moved back to disk unprotected. " +
                                  "Remove OOS_MASTERKEY_RELEASE from the environment.");
            if (actions.HasFlag(KeyringBootActions.PreviousKeyStillSet))
                logger.LogWarning("Keyring: OOS_MASTERKEY_PREVIOUS is set but was not needed. Remove it from the environment.");
        }
        
        _app.UseCors(_corsPolicy);

        _app.MigrateOosDatabases(Configuration!);

        // Configure the HTTP request pipeline.
        if (_app.Environment.IsDevelopment())
        {
            _app.EnsureDevEnvironmentReady().Wait()/*  */;
        }
        else
        {
            _app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            _app.UseHsts();
        }

        _app.UseHttpsRedirection();
        _app.UseStaticFiles(OpenOrderSystemApplication.UserStaticFiles);
        _app.UseStaticFiles();

        _app.UseRouting();

        _app.UseAuthorization();

        _app.UseMiddleware<InitialConfigAuth>();
        _app.UseMiddleware<PrinterBridgeAuth>();

        _app.MapV2ApiRoutes();

        var legacyComponents = Configuration!.GetConfig<string[]>("LEGACY_COMPONENTS");
        if (legacyComponents != null && legacyComponents.Contains("API/ALL"))
            _app.MapLegacyApiRoutes();
        
        _app.MapAreaControllerRoute(
            name: "identity",
            areaName: "Identity",
            pattern: "Identity/{controller}/{action=Index}/{id?}");
        _app.MapAreaControllerRoute(
            name: "staff",
            areaName: "Staff",
            pattern: "Staff/{controller}/{action=Index}/{id?}");

        _app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        PingUtility.RegisterData("Boot_Mode", _ => PingUtility.Value("Default"));
        
        return _app;
    }
    public IBootMode Initialize(string[] args, Configuration bootConfig)
    {
        _bob = WebApplication.CreateBuilder(args);
        
        Configuration = bootConfig;
        IsDevelopment = _bob.Environment.IsDevelopment();

        return this;
    }
    public IBootMode LoadServices()
    {

        if (_bob == null || Configuration == null)
            throw new InvalidOperationException("Unable to build OOS Server, missing builder! Please ensure Initialize is called before attempting to build.");

        var corsHosts = (_bob.Environment.IsDevelopment() ?
            _bob.Configuration.GetValue<string>("allowedOrigins:Development") :
            _bob.Configuration.GetValue<string>("allowedOrigins:Production")) ?? string.Empty;

        if (IsDevelopment)
        {
            _bob.Services.AddDatabaseDeveloperPageExceptionFilter();
        }
        else
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(OpenOrderSystemApplication.DataRootPath, "logs", "OOS_.log"))
                .CreateLogger();
            _bob.Services.AddSerilog();
        }
        

        _bob.Services.AddScoped<IEmailService, SmtpEmailService>();
        _bob.Services.AddScoped<ISmsService, DevSMS>();
        _bob.Services.AddScoped<PrinterService>();
        _bob.Services.AddScoped<PrinterBridgeAuth>();
        _bob.Services.AddTransient<MediaManagerService>();
        _bob.Services.AddSingleton<IEmailConfigService, InsecureEmailConfigService>();
        _bob.Services.AddSingleton<ConfigurationService>();  //legacy. TODO: depreciate
        _bob.Services.AddSingleton<InitialConfigAuth>();     //legacy. TODO: depreciate
        _bob.Services.AddSingleton<StaffTerminalMonitoringService>();
        _bob.Services.AddSingleton<CartService>();
        _bob.Services.AddSingleton<PrintSpoolerService>();
        _bob.Services.AddSingleton<IIconProvider, DefaultIconPack>();
        _bob.Services.AddSingleton<IAdminNavService, AdminNavService>();
        
        _bob.AddOosDatabaseServices(Configuration);
        _bob.AddOosConfigurationServices(_keyringBuildResult!); //null state ignored because preflight would have
                                                                //thrown and dropped to recovery mode.

        _bob.Services.AddHttpClient();
        _bob.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<OosDbContext>()
        .AddDefaultTokenProviders();

        _bob.Services.AddQuartz(q =>
        {
            var dailyKey = new JobKey(nameof(DailyCleanup));
            var customerKey = new JobKey(nameof(CustomerCleanup));
            var endOfDayKey = new JobKey(nameof(DailyReportPrint));

            var timezone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

            q.AddJob<DailyCleanup>(opts => opts.WithIdentity(dailyKey));
            q.AddJob<CustomerCleanup>(opts => opts.WithIdentity(customerKey));
            q.AddJob<DailyReportPrint>(opts => opts.WithIdentity(endOfDayKey));

            q.AddTrigger(opts => opts
                .ForJob(customerKey)
                .WithIdentity($"{nameof(CustomerCleanup)}-trigger")
                .WithDailyTimeIntervalSchedule(6, IntervalUnit.Hour)
            );

            q.AddTrigger(opts => opts
                .ForJob(dailyKey)
                .WithIdentity($"{nameof(DailyCleanup)}-trigger")
                .WithCronSchedule("0 59 23 * * ?", x => x.InTimeZone(timezone))
            );

            q.AddTrigger(opts => opts
                .ForJob(endOfDayKey)
                .WithIdentity($"{nameof(DailyReportPrint)}-trigger")
                .WithCronSchedule("0 0 21 * * ?", x => x.InTimeZone(timezone))
            );
        });

        _bob.Services.AddQuartzHostedService();
        _bob.Services.AddControllersWithViews();

        _bob.Services.AddCors(options =>
        {
            options.AddPolicy(name: _corsPolicy, policy =>
            {
                policy.WithOrigins(corsHosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) 
                    ?? Array.Empty<string>())
                .AllowAnyMethod()
                .AllowAnyHeader();
            });
        });

        return this;
    }

    public bool PreflightCheck(string[] args, Configuration bootConfig, out string[] errors)
    {
        var foundErrors = new List<string>();
        
        //attempt to open the keyring.
        try
        {
            _keyringBuildResult = KeyringBuilder.Build(OpenOrderSystemApplication.ProtectedRootPath);
        }
        catch (KeyringUnavailableException e)
        {
            foundErrors.Add(e.Message);
            _fallback = "recovery";
        }

        // In the prefab development environment a missing database is created by migrations at
        // startup. Elsewhere it must already exist (initial setup will handle creation).
        var allowMissingDatabase = PrefabDevelopmentEnvironmentExtensions.IsPrefabStandupEnabled();

        // Primary (V2) database connection check.
        var dbProvider = bootConfig.GetConfig<DbProviders>("DB_PROVIDER");
        var dbConnectionString = bootConfig.GetConfig("DB_CONNECTION_STRING");

        if (string.IsNullOrEmpty(dbConnectionString))
        {
            foundErrors.Add("Connection string not found in boot config, falling back to no-db-boot");

            _fallback = !bootConfig.SetupComplete ? "initial_setup" : "recovery";
        }
        else if (!dbProvider.IsAvailable())
        {
            var message = dbProvider.NotAvailable().Message;
            Log.Warning("Preflight: {Message}", message);
            foundErrors.Add(message);
            _fallback = "recovery";
        }
        else
        {
            var dbConnectionTools = new DatabaseConnectionTool<OosDbContext>(dbProvider, dbConnectionString ?? "");
            CheckDatabase("Database", dbConnectionTools, allowMissingDatabase, foundErrors);
        }

        // Legacy database connection check. Only when the legacy database is enabled; a leftover
        // connection string for a disabled component must not send boot to recovery.
        var legacyComponents = bootConfig.GetConfig<string[]>("LEGACY_COMPONENTS");
        var legacyDatabaseEnabled = legacyComponents != null && legacyComponents.Contains("DATABASE");
        var legacyProvider = bootConfig.GetConfig<DbProviders>("LEGACY:DB_PROVIDER");
        var legacyConnectionString = bootConfig.GetConfig("LEGACY:DB_CONNECTION_STRING");

        if (!legacyDatabaseEnabled)
        {
            // Nothing to check.
        }
        else if (string.IsNullOrEmpty(legacyConnectionString))
        {
            foundErrors.Add("Legacy database is enabled in LEGACY_COMPONENTS, but LEGACY:DB_CONNECTION_STRING is not set.");
            _fallback = "recovery";
        }
        else if (!legacyProvider.IsAvailable())
        {
            var message = $"Legacy database: {legacyProvider.NotAvailable().Message}";
            Log.Warning("Preflight: {Message}", message);
            foundErrors.Add(message);
            _fallback = "recovery";
        }
        else
        {
            var legacyConnectionTools = new DatabaseConnectionTool<ApplicationDbContext>(legacyProvider, legacyConnectionString);
            CheckDatabase("Legacy database", legacyConnectionTools, allowMissingDatabase, foundErrors);
        }

        errors = foundErrors.ToArray();
        return foundErrors.Count == 0;
    }

    /// <summary>
    /// Adds preflight errors for a database that is unreachable, or missing when creation isn't allowed.
    /// </summary>
    private static void CheckDatabase<TContext>(string label, DatabaseConnectionTool<TContext> tool, bool allowMissing, List<string> foundErrors)
        where TContext : DbContext
    {
        switch (tool.GetDatabaseStatus(out var statusErrors))
        {
            case DatabaseStatus.Available:
                break;

            case DatabaseStatus.Missing when allowMissing:
                // Serilog isn't configured during preflight; report alongside the boot screen instead.
                Console.WriteLine($"  ⚠  {label} does not exist yet; it will be created by migrations (prefab development environment).");
                break;

            case DatabaseStatus.Missing:
                foundErrors.Add($"{label} does not exist. Create it, or run initial setup, before starting OOS.");
                break;

            default:
                foundErrors.AddRange(statusErrors.Select(e => $"{label}: {e}"));
                break;
        }
    }
}
