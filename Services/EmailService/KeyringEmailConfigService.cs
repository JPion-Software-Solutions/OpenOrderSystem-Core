using OpenOrderSystem.Core.Services.EmailService.Exceptions;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService;

/// <summary>
/// Production implementation of <see cref="IEmailConfigService"/>. Values live in the config store; secrets
/// live in the keyring and are referenced from config values with <c>&lt;&lt;key:name&gt;&gt;</c> tokens, which
/// are expanded on read.
/// </summary>
/// <remarks>
/// <para>Only keys listed in <see cref="KeySources"/> are accepted; anything else throws, so a new key can't
/// silently end up stored in plain text.</para>
/// <para><see cref="KeySource.Secret"/> keys always resolve through the keyring. Their config value defaults to a
/// token for a keyring secret of the same name (e.g. <c>&lt;&lt;key:smtp:password&gt;&gt;</c>), so nothing has to be
/// seeded. They can't be written here: <see cref="SetKeyAsync{T}"/> throws <see cref="SecretConfigKeyException"/>
/// so the UI can send the admin to the keyring instead.</para>
/// <para>Any config value may contain tokens, so an admin can move other values into the keyring too.</para>
/// </remarks>
public class KeyringEmailConfigService : IEmailConfigService
{
    private readonly IConfigurationStore _config;
    private readonly IReadOnlyKeyring _keyring;
    private readonly ILogger<KeyringEmailConfigService> _logger;

    public KeyringEmailConfigService(IConfigurationStore config, IReadOnlyKeyring keyring, ILogger<KeyringEmailConfigService> logger)
    {
        _config = config;
        _keyring = keyring;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <exception cref="KeyNotFoundException">The key isn't a known email configuration key.</exception>
    public async Task<string?> GetKeyAsync<T>(string key)
    {
        var value = await _config.GetConfigurationAsync<string>(key);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        try
        {
            return _keyring.ExpandSecrets(value); //should passthrough any string without a key (all other configs)
        }
        catch (MissingSecretException ex)
        {
            // Names only; never log values.
            _logger.LogWarning("Email config key '{Key}' references keyring secret(s) that aren't set: {Secrets}.",
                key, string.Join(", ", ex.MissingKeys));
            return null;
        }
    }

    /// <inheritdoc/>
    /// <exception cref="KeyNotFoundException">The key isn't a known email configuration key.</exception>
    /// <exception cref="SecretConfigKeyException">The key's value lives in the keyring.</exception>
    public async Task SetKeyAsync<T>(string key, string value)
    {
        if (key == SmtpEmailService.ConfigKeys.Username)
            await _config.SetConfigurationAsync(SmtpEmailService.ConfigKeys.Password, IReadOnlyKeyring.TokenPrefix + SmtpEmailService.ConfigKeys.Password + IReadOnlyKeyring.TokenSuffix);
        
        if (key == SmtpEmailService.ConfigKeys.Password)
            throw new SecretConfigKeyException(key, key);

        if (await _config.SetConfigurationAsync(key, value) is null)
            throw new InvalidOperationException($"Email config key '{key}' is locked and can't be changed.");
    }
}
