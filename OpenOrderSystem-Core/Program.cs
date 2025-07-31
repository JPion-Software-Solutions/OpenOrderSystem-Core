using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Serilog;
using Microsoft.AspNetCore.DataProtection;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.Interfaces;
using OpenOrderSystem.Core.Quartz.AutomatedTasks;
using OpenOrderSystem.Core.Middleware;
using OpenOrderSystem.Core.Areas.API.Controllers;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Areas.Staff.Controllers.Manager;
using OpenOrderSystem.Core.Models;
using System.Reflection;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var bob = WebApplication.CreateBuilder(args);

        var connectionString = bob.Environment.IsDevelopment() ?
            bob.Configuration.GetConnectionString("Development") :
            bob.Configuration.GetConnectionString("Production");

        var dbPass = Environment.GetEnvironmentVariable("DB_PASS");
        connectionString = connectionString?.Replace("<<DB_PASS>>", dbPass ?? "");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File("logs/OOS_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        bob.Services.AddSerilog();

        bob.Services.AddScoped<IEmailService, DevEmail>();
        bob.Services.AddScoped<ISmsService, TwilioSmsService>();
        bob.Services.AddScoped<PrinterService>();
        bob.Services.AddScoped<PrinterBridgeAuth>();
        bob.Services.AddTransient<MediaManagerService>();
        bob.Services.AddSingleton<ConfigurationService>();
        bob.Services.AddSingleton<InitialConfigAuth>();
        bob.Services.AddSingleton<StaffTerminalMonitoringService>();
        bob.Services.AddSingleton<CartService>();
        bob.Services.AddSingleton<PrintSpoolerService>();

        bob.Services.AddDbContext<DataProtectionKeyContext>(options =>
            options.UseSqlServer(connectionString));

        bob.Services.AddDataProtection()
            .PersistKeysToDbContext<DataProtectionKeyContext>()
            .SetApplicationName("OpenOrderSystem");

        bob.Services.AddDbContext<ApplicationDbContext>(options =>
            { 
                options.UseSqlServer(connectionString);
                options.EnableDetailedErrors();
            });

        bob.Services.AddDatabaseDeveloperPageExceptionFilter();

        bob.Services.AddIdentity<IdentityUser, IdentityRole>
            (options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        bob.Services.AddQuartz(q =>
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


        bob.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        bob.Services.AddControllersWithViews();

        var app = bob.Build();

        //DBCleanup
        using (var scope = app.Services.CreateScope())
        {
            var dataKeyContext = scope.ServiceProvider.GetRequiredService<DataProtectionKeyContext>();
            dataKeyContext.Database.Migrate();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();

            //Set confirmation code count
            ConfirmationCode.CodesIssued = context.ConfirmationCodes.Count();
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.UseMiddleware<InitialConfigAuth>();
        app.UseMiddleware<PrinterBridgeAuth>();

        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        MenuAdminController.ImageDirectoryPath = Path.Combine(app.Environment.WebRootPath, "media", "images");
        MediaManagerService.MediaRootPath = Path.Combine(app.Environment.WebRootPath, "media");

        SystemController.Version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        SystemController.SystemBoot = DateTime.Now;

        app.Run();
    }
}