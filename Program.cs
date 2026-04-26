using System.Reflection;
using OpenOrderSystem.Core.Areas.API.Legacy.Controllers;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var oosApp = new OpenOrderSystemApplication();
        var app = await oosApp.BuildApp(args);

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