using System;
using System.ComponentModel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NuGet.Packaging;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Extensions.AspNet;
using OpenOrderSystem.Core.Extensions.Development;
using OpenOrderSystem.Core.Middleware;
using OpenOrderSystem.Core.Quartz.AutomatedTasks;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.EmailService;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;
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

    public string? RequestedFallback => _fallback;

    public WebApplicationBuilder? Bob => _bob;
    
    public WebApplication? App => _app;
    
    public bool IsDevelopment { get; private set; }

    public Configuration? Configuration { get; private set; }

    public WebApplication ConfigureMiddleware()
    {
        _app = _bob?.Build()
            ?? throw new InvalidOperationException("Unable to build OOS Server, missing builder! Please ensure Initialize is called before attempting to build.");

        _app.EnsureCoreUserRolesCreated().Wait();
        _app.UseCors(_corsPolicy);
        

        //DBCleanup
        using (var scope = _app.Services.CreateScope())
        {
            var dataKeyContext = scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>();
            dataKeyContext.Database.Migrate();

            var oosContext = scope.ServiceProvider.GetRequiredService<OosDbContext>();
            oosContext.Database.Migrate();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();

            //Set confirmation code count
            ConfirmationCode.CodesIssued = context.ConfirmationCodes.Count();
        }

        // Configure the HTTP request pipeline.
        if (_app.Environment.IsDevelopment())
        {
            _app.UseMigrationsEndPoint();
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

        _app.MapControllerRoute(
            name: "api",
            pattern: "API/{controller}/{action=Index}/{id?}");
        _app.MapControllerRoute(
            name: "identity",
            pattern: "Identity/{controller}/{action=Index}/{id?}");
        _app.MapControllerRoute(
            name: "staff",
            pattern: "Staff/{controller}/{action=Index}/{id?}");

        _app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        
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
                .WriteTo.File("logs/OOS_.log")
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

        _bob.Services.AddDbContext<DataProtectionKeyContext>(options => options.UseDynamicSQLProvider(Configuration));

        _bob.Services.AddDataProtection()
            .PersistKeysToDbContext<DataProtectionKeyContext>()
            .SetApplicationName("OpenOrderSystem");

        _bob.Services.AddDbContextFactory<OosDbContext>(options =>
        {
            options.UseDynamicSQLProvider(Configuration);
            options.EnableDetailedErrors();
        });

        _bob.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseDynamicSQLProvider(Configuration, "LEGACY");
            options.EnableDetailedErrors();
        });

        _bob.Services.AddHttpClient();
        _bob.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
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

        // Primary (V2) database connection check.
        var dbProvider = bootConfig.GetConfig<DbProviders>("DB_PROVIDER");
        var dbConnectionString = bootConfig.GetConfig("DB_CONNECTION_STRING");

        if (dbConnectionString.IsNullOrEmpty())
        {
            foundErrors.Add("Connection string not found in boot config, falling back to no-db-boot");

            if (bootConfig.SetupComplete)
                _fallback = "initial_setup";
            else
                _fallback = "recovery";
        }
        else
        {
            var dbConnectionTools = new DatabaseConnectionTool<OosDbContext>(dbProvider, dbConnectionString ?? "");
            dbConnectionTools.CanConnect(out var t);
            foundErrors.AddRange(t);
        }

        // Legacy database connection check.
        var legacyProvider = bootConfig.GetConfig<DbProviders>("LEGACY:DB_PROVIDER");
        var legacyConnectionString = bootConfig.GetConfig("LEGACY:DB_CONNECTION_STRING");

        if (!legacyConnectionString.IsNullOrEmpty())
        {
            var legacyConnectionTools = new DatabaseConnectionTool<ApplicationDbContext>(legacyProvider, legacyConnectionString ?? "");
            legacyConnectionTools.CanConnect(out var t);
            foundErrors.AddRange(t);
        }

        errors = foundErrors.ToArray();
        return foundErrors.Count == 0;
    }
}
