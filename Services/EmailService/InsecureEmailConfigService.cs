using System.Text.Json;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService;

/// <summary>
/// A development-only implementation of <see cref="IEmailConfigService"/> that persists
/// configuration as a plaintext JSON file on disk.
/// <para>
/// <strong>⚠ WARNING: This implementation is NOT suitable for production use.</strong>
/// Configuration values including SMTP credentials are stored in plaintext with no
/// encryption or access control. This class exists solely to support local development
/// and testing workflows without requiring a production config store.
/// </para>
/// <para>
/// The config file is intentionally placed in a path named
/// <c>config/DANGER_NOT_FOR_PRODUCTION/</c> to make its unsuitability for production
/// obvious. Ensure this directory is excluded from source control via <c>.gitignore</c>.
/// </para>
/// </summary>
public class InsecureEmailConfigService : IEmailConfigService
{
    private static readonly string ConfigDirectory = 
        Path.Combine("config", "DANGER_NOT_FOR_PRODUCTION");
    
    private static readonly string ConfigFilePath = 
        Path.Combine(ConfigDirectory, "dangerEmailConfig.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Retrieves a configuration value by key. The type parameter is accepted for
    /// interface compatibility but is not used for scoping in this implementation —
    /// all keys share a single flat namespace.
    /// </summary>
    /// <typeparam name="T">The calling service type. Unused in this implementation.</typeparam>
    /// <param name="key">The configuration key to retrieve.</param>
    /// <returns>The stored value, or <c>null</c> if the key does not exist.</returns>
    public string? GetKey<T>(string key)
    {
        var config = LoadConfig();
        return config.ContainsKey(key) ? config[key] : null;
    }

    /// <summary>
    /// Stores a configuration value by key, persisting it immediately to disk.
    /// The type parameter is accepted for interface compatibility but is not used
    /// for scoping in this implementation.
    /// </summary>
    /// <typeparam name="T">The calling service type. Unused in this implementation.</typeparam>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The value to store. Written to disk in plaintext.</param>
    public void SetKey<T>(string key, string value)
    {
        var config = LoadConfig();
        config[key] = value;
        SaveConfig(config);
    }

    /// <summary>
    /// Loads the config dictionary from disk, creating the config file with an empty
    /// dictionary if it does not yet exist.
    /// </summary>
    private Dictionary<string, string> LoadConfig()
    {
        if (!Directory.Exists(ConfigDirectory))
            Directory.CreateDirectory(ConfigDirectory);

        if (!File.Exists(ConfigFilePath))
            File.WriteAllText(ConfigFilePath, 
                JsonSerializer.Serialize(new Dictionary<string, string>(), JsonOptions));

        return JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(ConfigFilePath))!;
    }

    /// <summary>
    /// Persists the config dictionary to disk, overwriting the existing file.
    /// </summary>
    private void SaveConfig(Dictionary<string, string> config)
    {
        File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(config, JsonOptions));
    }
}