using System.Reflection;
using System.Text;
using Microsoft.Extensions.FileProviders;
using OpenOrderSystem.Core.Areas.API.Legacy.Controllers;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;

namespace OpenOrderSystem.Core.Bootstrapper;

public class OpenOrderSystemApplication
{
    private readonly Dictionary<string, Type> _bootModes = new(StringComparer.OrdinalIgnoreCase);

    public static StaticFileOptions UserStaticFiles { get; } = new();
    
    public static string DataRootPath { get; set; } = string.Empty;
    
    public Dictionary<string, Type> DiscoverBootModes()
    {
        _bootModes.Clear();

        var bootModeType = typeof(IBootMode);

        var candidates = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t =>
                t is { IsClass: true, IsAbstract: false } &&
                bootModeType.IsAssignableFrom(t));

        foreach (var type in candidates)
        {
            var key = ToSnakeCase(type.Name)
                .Replace("_bootstrap", "", StringComparison.OrdinalIgnoreCase);

            if (_bootModes.ContainsKey(key))
                throw new InvalidOperationException(
                    $"Duplicate boot mode key '{key}' discovered. Conflicting type: {type.FullName} Existing type: {_bootModes[key].FullName}");

            _bootModes[key] = type;
        }

        return _bootModes;
    }

    public async Task<WebApplication> BuildApp(string[] args)
    {
        DiscoverBootModes();
        var config = await Configuration.Load();
        var bootloader = GetBootMode(config.BootMode);
        
        if (!bootloader.PreflightCheck(args, config, out var err))
        {
            throw new InvalidOperationException($"Unable to boot OOS using bootmode '{config.BootMode}'. The following errors reported: {string.Join(',', err)}");
        }

        bootloader.Initialize(args, config);
        if (bootloader.Bob is null)
            throw new InvalidOperationException("Boot mode could not be initialized.");
        
        var userWwwroot = Path.Combine(DataRootPath, "public", "wwwroot");
        
        var inDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        var isDataRootSet = Environment.GetEnvironmentVariable("OOS_DATAROOT") is not null;
        
        if (!Directory.Exists(userWwwroot)) Directory.CreateDirectory(userWwwroot);
        
        UserStaticFiles.FileProvider = new PhysicalFileProvider(userWwwroot);
        
        Bootscreen(config.BootMode, inDocker, isDataRootSet);

        return bootloader
            .LoadServices()
            .ConfigureMiddleware();

    }

    public IBootMode GetBootMode(string key)
    {
        if (!_bootModes.TryGetValue(key, out var modeType))
            throw new InvalidOperationException($"Unable to boot OOS into mode: {key}");

        if (!typeof(IBootMode).IsAssignableFrom(modeType))
            throw new InvalidOperationException($"Type '{modeType.FullName}' is not a valid IBootMode (key: {key}).");

        // Create an instance of the boot mode
        var instance = Activator.CreateInstance(modeType)
            ?? throw new InvalidOperationException($"Failed to create boot mode instance for '{modeType.FullName}' (key: {key}).");

        return (IBootMode)instance;
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var sb = new StringBuilder(name.Length + 8);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if (char.IsUpper(c))
            {
                // add underscore if:
                // - not first char
                // - previous is lower OR previous is digit
                // - OR next exists and is lower (handles "HTTPServer" -> "http_server")
                var hasPrev = i > 0;
                var prev = hasPrev ? name[i - 1] : '\0';
                var hasNext = i + 1 < name.Length;
                var next = hasNext ? name[i + 1] : '\0';

                var shouldUnderscore =
                    hasPrev &&
                    (char.IsLower(prev) || char.IsDigit(prev) || (hasNext && char.IsLower(next)));

                if (shouldUnderscore)
                    sb.Append('_');

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    private void Bootscreen(string bootMode, bool inDocker, bool dataRootFromEnv)
    {
        Console.WriteLine();
        Console.WriteLine(@"   ____  ____  _____");
        Console.WriteLine(@"  / __ \/ __ \/ ___/");
        Console.WriteLine(@" / / / / / / /\__ \ ");
        Console.WriteLine(@"/ /_/ / /_/ /___/ / ");
        Console.WriteLine(@"\____/\____//____/  ");
        Console.WriteLine();
        Console.WriteLine("  Open Order System");
        Console.WriteLine("  ─────────────────────────────");
        Console.WriteLine($"  Version:     {SystemController.Version}");
        Console.WriteLine($"  Boot Mode:   {bootMode}");
        Console.WriteLine($"  Environment: {(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")}");
        Console.WriteLine($"  OS:          {Environment.OSVersion.Platform}");
        Console.WriteLine($"  OS Version:  {Environment.OSVersion.VersionString}");
        Console.WriteLine($"  Data Root:   {(dataRootFromEnv ? DataRootPath : "NOT SET")}");
        Console.WriteLine("  ─────────────────────────────");
        Console.WriteLine();

        if (inDocker && !dataRootFromEnv)
        {
            Console.Beep();
            Console.Beep();
            Console.WriteLine("  ⚠  Running in Docker but OOS_DATAROOT is not set — data will not persist!");
        }
        
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            Console.Beep();
            Console.Beep();
            Console.WriteLine("  ⚠  DEVELOPMENT ENVIRONMENT ACTIVE: This is a development environment not suitable for production use!");
        }
    }
}
