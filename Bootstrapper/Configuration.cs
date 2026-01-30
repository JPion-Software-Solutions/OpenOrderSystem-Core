using System.Reflection;
using System.Text.Json;

namespace OpenOrderSystem.Core.Bootstrapper;

/// <summary>
/// Stores Tier 0 configurations needed for bootstrapping the app.
/// </summary>
public sealed class Configuration
{
    // =========================
    // Static - private
    // =========================

    private static readonly StringComparer _keyComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> _readOnlyExactKeys = new(_keyComparer)
    {
        "OOS_INSTANCE_ID",
        "OOS_SETUP_COMPLETE",
        "OOS_LAST_BOOT_VERSION",
    };

    private static readonly string[] _readOnlyPrefixes =
    {
        "OOS_SYS_"
    };

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    // =========================
    // Static - public
    // =========================

    public static string ConfigurationDirectory { get; set; } = "config";

    // =========================
    // Non-static - private (fields)
    // =========================

    private readonly Dictionary<string, JsonElement> _configData;

    // =========================
    // Non-static - public (properties)
    // =========================

    public Guid InstanceId => GetRequiredGuid("OOS_INSTANCE_ID");

    public bool SetupComplete => GetRequiredBool("OOS_SETUP_COMPLETE");

    public string LastBootVersion => GetRequiredString("OOS_LAST_BOOT_VERSION");

    // =========================
    // Non-static - private (constructors)
    // =========================

    private Configuration(Dictionary<string, JsonElement> data)
    {
        _configData = data;
    }

    // =========================
    // Static - private (methods)
    // =========================

    private static string GetFilePath()
        => Path.Combine(ConfigurationDirectory, "oos.bootstrap.json");

    private static string GetAppVersionString()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v?.ToString() ?? "0.0.0";
    }

    private static void WriteAtomic(string filePath, object data)
    {
        var tmpPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(data, _serializerOptions);

        File.WriteAllText(tmpPath, json);

        // Replace in one step (rename is atomic on most filesystems when same volume)
        File.Move(tmpPath, filePath, overwrite: true);
    }

    private static async Task WriteAtomicAsync(string filePath, object data)
    {
        var tmpPath = filePath + ".tmp";
        var json = JsonSerializer.Serialize(data, _serializerOptions);

        await File.WriteAllTextAsync(tmpPath, json);

        // Replace in one step (rename is atomic on most filesystems when same volume)
        File.Move(tmpPath, filePath, overwrite: true);
    }

    private static Exception Missing(string key)
        => new InvalidOperationException($"Invalid bootstrap configuration: missing required key '{key}'.");

    private static Exception Invalid(string key)
        => new InvalidOperationException($"Invalid bootstrap configuration: key '{key}' has an invalid value.");

    // =========================
    // Static - public (methods)
    // =========================

    /// <summary>
    /// Loads bootstrap configuration from file or creates one if not already active.
    /// </summary>
    /// <returns>An active bootstrapper configuration profile.</returns>
    public static async Task<Configuration> Load()
    {
        Directory.CreateDirectory(ConfigurationDirectory);

        var filePath = GetFilePath();

        if (!File.Exists(filePath))
        {
            var seed = new Dictionary<string, object>(_keyComparer)
            {
                ["OOS_INSTANCE_ID"] = Guid.NewGuid(),
                ["OOS_SETUP_COMPLETE"] = false,
                ["OOS_LAST_BOOT_VERSION"] = GetAppVersionString()
            };

            await WriteAtomicAsync(filePath, seed);
        }

        var json = await File.ReadAllTextAsync(filePath);

        Dictionary<string, JsonElement>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, _serializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Bootstrap config is corrupted or invalid JSON: '{filePath}'.", ex);
        }

        return new Configuration(parsed ?? new Dictionary<string, JsonElement>(_keyComparer));
    }

    // =========================
    // Non-static - private (methods)
    // =========================

    private void EnsureWritableKey(string key)
    {
        if (_readOnlyExactKeys.Contains(key))
            throw new InvalidOperationException($"The bootstrap config key '{key}' is system-reserved and read-only.");

        if (_readOnlyPrefixes.Any(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"The bootstrap config key '{key}' is system-reserved and read-only.");
    }

    private Guid GetRequiredGuid(string key)
    {
        if (!_configData.TryGetValue(key, out var el)) throw Missing(key);

        // Accept either "guid-string" or JSON Guid
        if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var g)) return g;

        try
        {
            return el.Deserialize<Guid>(_serializerOptions);
        }
        catch
        {
            throw Invalid(key);
        }
    }

    private bool GetRequiredBool(string key)
    {
        if (!_configData.TryGetValue(key, out var el)) throw Missing(key);

        if (el.ValueKind == JsonValueKind.True) return true;
        if (el.ValueKind == JsonValueKind.False) return false;

        if (el.ValueKind == JsonValueKind.String && bool.TryParse(el.GetString(), out var b)) return b;

        throw Invalid(key);
    }

    private string GetRequiredString(string key)
    {
        if (!_configData.TryGetValue(key, out var el)) throw Missing(key);

        if (el.ValueKind == JsonValueKind.String) return el.GetString()!;
        return el.GetRawText();
    }

    // =========================
    // Non-static - public (methods)
    // =========================

    public T? GetConfig<T>(string key)
    {
        if (!_configData.TryGetValue(key, out var el)) return default;
        return el.Deserialize<T>(_serializerOptions);
    }

    public string? GetConfig(string key)
    {
        if (!_configData.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
    }

    public Configuration SetConfig(string key, object value, bool autosave = false)
    {
        EnsureWritableKey(key);

        _configData[key] = JsonSerializer.SerializeToElement(value, _serializerOptions);

        if (autosave)
            SaveChanges();

        return this;
    }

    public void SaveChanges()
    {
        Directory.CreateDirectory(ConfigurationDirectory);

        var filePath = GetFilePath();
        WriteAtomic(filePath, _configData);
    }

    public async Task SaveChangesAsync()
    {
        Directory.CreateDirectory(ConfigurationDirectory);

        var filePath = GetFilePath();
        await WriteAtomicAsync(filePath, _configData);
    }
}
