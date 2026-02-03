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
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Areas.Staff.Controllers.Manager;
using OpenOrderSystem.Core.Models;
using System.Reflection;
using OpenOrderSystem.Core.Areas.API.Legacy.Controllers;
using OpenOrderSystem.Core.Extensions.AspNet;
using OpenOrderSystem.Core.Extensions.Development;
using OpenOrderSystem.Core.Bootstrapper;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var OOSApp = new OpenOrderSystemApplication();
        var app = OOSApp.BuildApp(args).Result;

        OpenOrderSystem.Core.Areas.Staff.Controllers.Manager.MenuController.ImageDirectoryPath = Path.Combine(app.Environment.WebRootPath, "media", "images");
        MediaManagerService.MediaRootPath = Path.Combine(app.Environment.WebRootPath, "media");

        SystemController.Version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

        if (SystemController.Version.Contains('+'))
            SystemController.Version = SystemController.Version.Remove(SystemController.Version.IndexOf('+'));
        
        SystemController.SystemBoot = DateTime.Now;

        app.Run();
    }
}