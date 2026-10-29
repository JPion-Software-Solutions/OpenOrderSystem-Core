using System.Reflection;
using OpenOrderSystem.Core.Areas.API.Legacy.Controllers;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Services.Legacy;

namespace OpenOrderSystem.Core;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        OpenOrderSystemApplication.DataRootPath = Environment.GetEnvironmentVariable("OOS_DATAROOT") ??
                                                  Path.Combine(AppContext.BaseDirectory, "appdata");
        
        OpenOrderSystemApplication.ProtectedRootPath = Environment.GetEnvironmentVariable("OOS_PROTECTEDROOT")
                                                       ?? DefaultProtectedRoot();
        
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

    /// <summary>
    /// Default location for the keyring (DEK + secrets), kept apart from the data root. Privileged
    /// processes use the machine-wide location (on Linux, /var/lib/oos matches systemd
    /// StateDirectory=oos); unprivileged ones fall back to the per-user data folder
    /// (%LOCALAPPDATA% on Windows, $XDG_DATA_HOME or ~/.local/share on Linux).
    /// </summary>
    private static string DefaultProtectedRoot()
    {
        if (Environment.IsPrivilegedProcess)
            return OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OpenOrderSystem")
                : "/var/lib/oos";

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            OperatingSystem.IsWindows() ? "OpenOrderSystem" : "oos");
    }
}