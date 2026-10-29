namespace OpenOrderSystem.Core.Services.EmailService.Exceptions;

/// <summary>
/// Thrown when code tries to set a configuration key whose value lives in the keyring.
/// The UI should catch this and point the admin to the keyring, where <see cref="SecretName"/> is set.
/// </summary>
public class SecretConfigKeyException : InvalidOperationException
{
    /// <summary>The configuration key that was being set.</summary>
    public string Key { get; }

    /// <summary>The keyring secret that holds the value.</summary>
    public string SecretName { get; }

    public SecretConfigKeyException(string key, string secretName)
        : base($"'{key}' is stored in the keyring as '{secretName}'. Set it through the keyring, not the email configuration.")
    {
        Key = key;
        SecretName = secretName;
    }
}
