using System.Reflection;
using System.Text;
using OpenOrderSystem.Core.Bootstrapper.Interfaces;

namespace OpenOrderSystem.Core.Bootstrapper;

public class OpenOrderSystemApplication
{
    private readonly Dictionary<string, Type> _bootModes = new(StringComparer.OrdinalIgnoreCase);

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
        var bootmode = GetBootMode(config.BootMode);
        
        if (!bootmode.PreflightCheck(args, config, out var err))
        {
            throw new InvalidOperationException($"Unable to boot OOS using bootmode '{config.BootMode}'. The following errors reported: {string.Join(',', err)}");
        }

        return bootmode
            .Initialize(args, config)
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
}
