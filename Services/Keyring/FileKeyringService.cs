using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;

namespace OpenOrderSystem.Core.Services.Keyring;

/// <summary>
/// <see cref="IKeyringService"/> backed by <c>keyring.json</c> in the protected root. Created only by
/// <see cref="KeyringBuilder"/>; register the one instance as a singleton for both
/// <see cref="IKeyringService"/> and <see cref="IReadOnlyKeyringService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Secrets stay encrypted in memory and are decrypted on each read. Reads use an immutable
/// snapshot and never block. Writes are serialized by a lock, persisted atomically, and only
/// then published, so a failed write throws and changes nothing.
/// </para>
/// <para>
/// Secret names are case-sensitive.
/// </para>
/// <para>
/// Every write member (<see cref="SetSecret"/>, <see cref="DeleteSecret"/>, <see cref="EnableEncryption"/>,
/// <see cref="RotateDataKey"/>) throws <see cref="KeyringStorageException"/> if the file cannot be
/// saved; the keyring is then unchanged.
/// </para>
/// </remarks>
public sealed partial class FileKeyringService : IKeyringService
{
    public const int MaxKeyNameLength = 200;

    private readonly string _path;
    private readonly EnvironmentKeyContext? _environment;
    private readonly TimeProvider _time;
    private readonly Lock _writeLock = new();
    private volatile KeyringState _state;

    internal FileKeyringService(string path, KeyringState state, EnvironmentKeyContext? environment, TimeProvider time)
    {
        _path = path;
        _state = state;
        _environment = environment;
        _time = time;
    }

    /// <summary>Full path of the keyring file.</summary>
    public string FilePath => _path;

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyNamePattern();

    /// <summary>Whether <paramref name="key"/> is a legal secret name: <c>[A-Za-z0-9._-]</c>, 1–200 characters.</summary>
    public static bool IsValidKeyName(string? key)
        => !string.IsNullOrEmpty(key) && key.Length <= MaxKeyNameLength && KeyNamePattern().IsMatch(key);

    // ------------------------------------------------------------------ IReadOnlyKeyringService

    public string GetSecret(string key)
        => TryGetSecret(key, out var value) ? value : throw new MissingSecretException(key);

    public bool TryGetSecret(string key, out string value)
    {
        var state = _state;
        if (key is null || !state.Secrets.TryGetValue(key, out var secret))
        {
            value = string.Empty;
            return false;
        }

        value = Reveal(state, secret);
        return true;
    }

    public bool HasSecret(string key) => key is not null && _state.Secrets.ContainsKey(key);

    public IReadOnlyCollection<string> GetSecretNames() => _state.Secrets.Keys.ToList();

    // ------------------------------------------------------------------ IKeyringService

    public KeyringStatus GetStatus()
    {
        var state = _state;
        var warnings = KeyringWarnings.None;

        switch (state.Protection)
        {
            case KeyProtection.None:
                warnings |= KeyringWarnings.PassThrough;
                break;
            case KeyProtection.DataKeyOnDisk:
                warnings |= KeyringWarnings.DataKeyUnwrapped;
                break;
            case KeyProtection.EnvironmentKek when _environment!.IsWeak:
                warnings |= KeyringWarnings.WeakEnvironmentKey;
                break;
        }

        if (state.CurrentVersion is { } current && state.Secrets.Values.Any(s => s.DataKeyVersion != current))
            warnings |= KeyringWarnings.StaleDataKeyVersions;

        return new KeyringStatus(
            state.Protection,
            state.CurrentVersion,
            state.Protection == KeyProtection.EnvironmentKek ? _environment!.Fingerprint : null,
            warnings);
    }

    public void SetSecret(string key, string value)
    {
        if (!IsValidKeyName(key))
            throw new ArgumentException(
                $"Secret names must be 1–{MaxKeyNameLength} characters from [A-Za-z0-9._-].", nameof(key));
        ArgumentNullException.ThrowIfNull(value);

        Write(state =>
        {
            var secret = Store(state, key, value, _time.GetUtcNow());
            return state with { Secrets = state.Secrets.SetItem(key, secret) };
        });
    }

    public bool DeleteSecret(string key)
    {
        var deleted = false;
        Write(state =>
        {
            if (key is null || !state.Secrets.ContainsKey(key))
                return state;
            deleted = true;
            return state with { Secrets = state.Secrets.Remove(key) };
        });
        return deleted;
    }

    public SecretInfo? GetSecretInfo(string key)
        => key is not null && _state.Secrets.TryGetValue(key, out var s)
            ? new SecretInfo(s.Key, s.DataKeyVersion, s.UpdatedAt)
            : null;

    public int EnableEncryption()
    {
        var count = 0;
        Write(state =>
        {
            if (state.Protection != KeyProtection.None)
                throw new InvalidOperationException("Encryption is already enabled.");
            count = state.Secrets.Count;
            return EncryptPassThrough(state, KeyProtection.DataKeyOnDisk);
        });
        return count;
    }

    public int RotateDataKey()
    {
        var count = 0;
        Write(state =>
        {
            if (state.Protection == KeyProtection.None)
                throw new InvalidOperationException("The keyring is in pass-through mode; there is no data key to rotate.");

            // Decrypt everything first: if any secret is unreadable, nothing changes.
            var plaintexts = state.Secrets.Values.Select(s => (Secret: s, Value: Reveal(state, s))).ToList();

            var version = state.DataKeys.Keys.Max() + 1;
            var dataKey = NewDataKey(version, _environment?.Kek);
            var rotated = state with
            {
                CurrentVersion = version,
                DataKeys = ImmutableSortedDictionary<int, DataKey>.Empty.Add(version, dataKey)
            };

            var secrets = rotated.Secrets;
            foreach (var (secret, value) in plaintexts)
                secrets = secrets.SetItem(secret.Key, Store(rotated, secret.Key, value, secret.UpdatedAt));

            count = plaintexts.Count;
            return rotated with { Secrets = secrets };
        });
        return count;
    }

    public bool MatchesEnvironmentKey(string candidate)
    {
        if (_environment is null || _state.Protection != KeyProtection.EnvironmentKek || string.IsNullOrWhiteSpace(candidate))
            return false;

        var kdf = _environment.Kdf;
        var derived = KeyringCrypto.DeriveKek(candidate, kdf.Salt, kdf.Iterations);
        try
        {
            return CryptographicOperations.FixedTimeEquals(derived, _environment.Kek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derived);
        }
    }

    // ------------------------------------------------------------------ internals

    private void Write(Func<KeyringState, KeyringState> mutate)
    {
        lock (_writeLock)
        {
            var current = _state;
            var next = mutate(current);
            if (ReferenceEquals(next, current))
                return;

            try
            {
                KeyringStore.Save(_path, next, _environment);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new KeyringStorageException(_path, ex);
            }

            _state = next;
        }
    }

    /// <summary>Decrypts a stored secret, or returns its value in pass-through mode.</summary>
    /// <exception cref="KeyringDecryptionException">The ciphertext or its data key is bad.</exception>
    internal static string Reveal(KeyringState state, StoredSecret secret)
    {
        if (secret.DataKeyVersion is not { } version)
            return secret.Value!;

        try
        {
            if (!state.DataKeys.TryGetValue(version, out var dataKey))
                throw new CryptographicException($"Data key version {version} is not in the keyring.");

            var plaintext = KeyringCrypto.Open(dataKey.Key, secret.Nonce, secret.Ciphertext,
                KeyringCrypto.SecretAad(secret.Key, version));
            try
            {
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        catch (CryptographicException ex)
        {
            throw new KeyringDecryptionException(secret.Key, ex);
        }
    }

    /// <summary>Builds the stored form of a value for the state's current mode and data key.</summary>
    internal static StoredSecret Store(KeyringState state, string key, string value, DateTimeOffset updatedAt)
    {
        if (state.CurrentVersion is not { } version)
            return new StoredSecret(key, null, null, null, value, updatedAt);

        var plaintext = Encoding.UTF8.GetBytes(value);
        try
        {
            var (nonce, ciphertext) = KeyringCrypto.Seal(state.DataKeys[version].Key, plaintext,
                KeyringCrypto.SecretAad(key, version));
            return new StoredSecret(key, version, nonce, ciphertext, null, updatedAt);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>A new random data key, wrapped with <paramref name="kek"/> when one is given.</summary>
    internal static DataKey NewDataKey(int version, byte[]? kek)
    {
        var key = KeyringCrypto.NewKey();
        return kek is null ? new DataKey(version, key) : Wrap(new DataKey(version, key), kek);
    }

    internal static DataKey Wrap(DataKey dataKey, byte[] kek)
    {
        var (nonce, wrapped) = KeyringCrypto.Seal(kek, dataKey.Key, KeyringCrypto.DataKeyAad(dataKey.Version));
        return dataKey with { Nonce = nonce, Wrapped = wrapped };
    }

    /// <summary>
    /// Leaves pass-through: creates data key version 1 (wrapped when <paramref name="kek"/> is given)
    /// and encrypts every plain-text secret, keeping their timestamps.
    /// </summary>
    internal static KeyringState EncryptPassThrough(KeyringState state, KeyProtection protection, byte[]? kek = null)
    {
        const int version = 1;
        var encrypted = state with
        {
            Protection = protection,
            CurrentVersion = version,
            DataKeys = ImmutableSortedDictionary<int, DataKey>.Empty.Add(version, NewDataKey(version, kek))
        };

        var secrets = encrypted.Secrets;
        foreach (var s in state.Secrets.Values)
            secrets = secrets.SetItem(s.Key, Store(encrypted, s.Key, s.Value!, s.UpdatedAt));

        return encrypted with { Secrets = secrets };
    }
}
