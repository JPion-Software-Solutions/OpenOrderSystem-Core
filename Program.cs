using System.Reflection;
using OpenOrderSystem.Core.Areas.API.Legacy.Controllers;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        OpenOrderSystemApplication.DataRootPath = Environment.GetEnvironmentVariable("OOS_DATAROOT") ??
                                                  Path.Combine(AppContext.BaseDirectory, "appdata");
        
        SystemController.Version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

        if (SystemController.Version.Contains('+'))
            SystemController.Version = SystemController.Version.Remove(SystemController.Version.IndexOf('+'));
        
        SystemController.SystemBoot = DateTime.Now;

        var oosApp = new OpenOrderSystemApplication();
        var app = await oosApp.BuildApp(args);

        
        MediaManagerService.MediaRootPath = Path.Combine(OpenOrderSystemApplication.DataRootPath, "public", "wwwroot", "media");
        OpenOrderSystem.Core.Areas.Staff.Controllers.Manager.MenuController.ImageDirectoryPath = Path.Combine(MediaManagerService.MediaRootPath, "images");
        
        app.Run();
    }
}