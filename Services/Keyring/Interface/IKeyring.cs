namespace OpenOrderSystem.Core.Services.Keyring.Interfaces;

/// <summary>
/// Full keyring access: everything in <see cref="IReadOnlyKeyring"/> plus writes and
/// maintenance. Only the keyring admin path should depend on this interface; everything
/// else, including the config service, depends on <see cref="IReadOnlyKeyring"/>.
/// </summary>
/// <remarks>
/// <para>
/// Secrets are encrypted with a data key (DEK). In <see cref="KeyProtection.DataKeyOnDisk"/>
/// mode the DEK is stored unwrapped in the protected root. In
/// <see cref="KeyProtection.EnvironmentKek"/> mode it is wrapped by a key-encryption key
/// (KEK) derived from an environment variable. The KEK is whatever that variable holds;
/// it is never stored or known by OOS beyond the running process.
/// </para>
/// <para>
/// Migrating to environment protection, replacing the environment key, and upgrading KDF
/// parameters all happen at boot in the keyring builder, since the environment is only
/// read at process start. This interface covers what can change while running.
/// </para>
/// <para>
/// Writes persist to the keyring file and take effect immediately for in-process readers.
/// Values already expanded into boot-time consumers (such as the database connection
/// string) pick up changes on the next restart. No member ever returns a secret value.
/// </para>
/// </remarks>
public interface IKeyring : IReadOnlyKeyring
{
    /// <summary>
    /// Current protection mode, data key version, environment key fingerprint, and any
    /// warnings for the admin console and nag system.
    /// </summary>
    KeyringStatus GetStatus();

    /// <summary>
    /// Creates the named secret, or replaces its value if it already exists.
    /// Encrypted with the current data key unless the keyring is in pass-through mode.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The key name is empty or contains characters outside <c>[A-Za-z0-9._-]</c>.
    /// </exception>
    void SetSecret(string key, string value);

    /// <summary>
    /// Deletes the named secret. Returns false if it did not exist.
    /// </summary>
    bool DeleteSecret(string key);

    /// <summary>
    /// Returns metadata for the named secret, or null if it does not exist.
    /// Never includes the value.
    /// </summary>
    SecretInfo? GetSecretInfo(string key);

    /// <summary>
    /// Leaves pass-through mode: generates the first data key, stores it on disk, and
    /// encrypts every existing plaintext secret. Returns the number of secrets encrypted.
    /// </summary>
    /// <exception cref="InvalidOperationException">Encryption is already enabled.</exception>
    int EnableEncryption();

    /// <summary>
    /// Generates a new data key, re-encrypts every secret with it, then retires the old
    /// one. Under environment protection the new key is wrapped with the current KEK.
    /// No environment change or restart is required. Returns the number of secrets rewritten.
    /// </summary>
    /// <exception cref="InvalidOperationException">The keyring is in pass-through mode.</exception>
    /// <exception cref="KeyringDecryptionException">An existing secret could not be decrypted.</exception>
    int RotateDataKey();

    /// <summary>
    /// Returns whether <paramref name="candidate"/> derives the same KEK as the one this
    /// process booted with. Lets an admin confirm a saved copy of the environment key is
    /// correct without OOS storing it. Always false outside environment protection.
    /// </summary>
    bool MatchesEnvironmentKey(string candidate);
}

/// <summary>How the data key is protected at rest.</summary>
public enum KeyProtection
{
    /// <summary>No data key exists; secrets are stored unencrypted.</summary>
    None,

    /// <summary>The data key is stored unwrapped in the protected root.</summary>
    DataKeyOnDisk,

    /// <summary>The data key is wrapped by a KEK derived from an environment variable.</summary>
    EnvironmentKek
}

/// <summary>Conditions the admin console should surface; flags may combine.</summary>
[Flags]
public enum KeyringWarnings
{
    None = 0,

    /// <summary>Secrets are stored unencrypted.</summary>
    PassThrough = 1,

    /// <summary>The data key is stored unencrypted in the protected root.</summary>
    DataKeyUnwrapped = 2,

    /// <summary>The environment key looks like a short passphrase rather than a generated key.</summary>
    WeakEnvironmentKey = 4,

    /// <summary>Some secrets are still on an older data key version (an interrupted rotation).</summary>
    StaleDataKeyVersions = 8
}

/// <summary>
/// Snapshot of keyring state for display. Contains no key material.
/// </summary>
/// <param name="Protection">How the data key is protected.</param>
/// <param name="DataKeyVersion">Version of the current data key, or null in pass-through mode.</param>
/// <param name="EnvironmentKeyFingerprint">
/// Short fingerprint of the derived KEK (first 8 hex characters of its SHA-256) for
/// comparison against saved copies, or null outside environment protection.
/// </param>
/// <param name="Warnings">Conditions to surface through the nag system.</param>
public sealed record KeyringStatus(
    KeyProtection Protection,
    int? DataKeyVersion,
    string? EnvironmentKeyFingerprint,
    KeyringWarnings Warnings);

/// <summary>
/// Value-free description of a stored secret, safe to display to admins.
/// </summary>
/// <param name="Key">The secret's name.</param>
/// <param name="DataKeyVersion">The data key version it is encrypted with, or null if stored unencrypted.</param>
/// <param name="UpdatedAt">When the value was last written.</param>
public sealed record SecretInfo(string Key, int? DataKeyVersion, DateTimeOffset UpdatedAt);

/// <summary>
/// Thrown when a secret exists but cannot be decrypted: the ciphertext is corrupted or
/// tampered with, or the data key does not match. The message names the secret only,
/// never its bytes.
/// </summary>
public sealed class KeyringDecryptionException : Exception
{
    public string Key { get; }

    public KeyringDecryptionException(string key, Exception? inner = null)
        : base($"Keyring secret '{key}' could not be decrypted. " +
               "It may be corrupted, or the data key may not match.", inner)
    {
        Key = key;
    }
}