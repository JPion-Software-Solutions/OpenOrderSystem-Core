using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;
using OpenOrderSystem.Core.Services.Keyring.Model;

namespace OpenOrderSystem.Core.Services.Keyring;

/// <summary>A data key in memory. <see cref="Nonce"/>/<see cref="Wrapped"/> are set under environment protection.</summary>
internal sealed record DataKey(int Version, byte[] Key, byte[]? Nonce = null, byte[]? Wrapped = null);

/// <summary>A secret in memory, still encrypted (or plain text in pass-through mode).</summary>
internal sealed record StoredSecret(
    string Key, int? DataKeyVersion, byte[]? Nonce, byte[]? Ciphertext, string? Value, DateTimeOffset UpdatedAt);

/// <summary>KEK derivation parameters.</summary>
internal sealed record KdfParameters(string Algorithm, int Iterations, byte[] Salt);

/// <summary>
/// The KEK for this process, derived at boot. Exists only under environment protection.
/// </summary>
internal sealed record EnvironmentKeyContext(byte[] Kek, KdfParameters Kdf, string Fingerprint, bool IsWeak);

/// <summary>Immutable keyring contents. Replaced wholesale on every write.</summary>
internal sealed record KeyringState(
    KeyProtection Protection,
    int? CurrentVersion,
    ImmutableSortedDictionary<int, DataKey> DataKeys,
    ImmutableSortedDictionary<string, StoredSecret> Secrets)
{
    public static KeyringState Empty { get; } = new(
        KeyProtection.None, null,
        ImmutableSortedDictionary<int, DataKey>.Empty,
        ImmutableSortedDictionary.Create<string, StoredSecret>(StringComparer.Ordinal));
}

/// <summary>Reads and writes <c>keyring.json</c>.</summary>
internal static class KeyringStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerOnlyDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>Returns the file, or null if it does not exist. Validates its structure.</summary>
    /// <exception cref="KeyringUnavailableException">Unreadable, malformed, or from a newer OOS.</exception>
    public static KeyringFile? Load(string path)
    {
        KeyringFile? file;
        try
        {
            if (!File.Exists(path))
                return null;

            using var stream = File.OpenRead(path);
            file = JsonSerializer.Deserialize<KeyringFile>(stream, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw Corrupt(path, "it is not valid JSON", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new KeyringUnavailableException(KeyringUnavailableReason.StorageError,
                $"The keyring file '{path}' could not be read: {ex.Message}", ex);
        }

        if (file is null)
            throw Corrupt(path, "it is empty");
        if (file.Format > KeyringFile.CurrentFormat)
            throw new KeyringUnavailableException(KeyringUnavailableReason.UnsupportedFormat,
                $"The keyring file '{path}' has format {file.Format}; this version of OOS understands up to " +
                $"{KeyringFile.CurrentFormat}. Upgrade OOS rather than editing the file.");

        Validate(path, file);
        return file;
    }

    private static void Validate(string path, KeyringFile file)
    {
        if (file.Format < 1)
            throw Corrupt(path, $"format {file.Format} is not valid");
        if (!Enum.IsDefined(file.Protection))
            throw Corrupt(path, "the protection mode is unknown");

        var versions = new HashSet<int>();
        foreach (var dk in file.DataKeys)
        {
            if (!versions.Add(dk.Version))
                throw Corrupt(path, $"data key version {dk.Version} appears twice");

            var ok = file.Protection switch
            {
                KeyProtection.DataKeyOnDisk => dk.Key is { Length: KeyringCrypto.KeySize } && dk.Wrapped is null,
                KeyProtection.EnvironmentKek => dk.Key is null && dk.Nonce is { Length: KeyringCrypto.NonceSize }
                                                && dk.Wrapped is { Length: KeyringCrypto.KeySize + KeyringCrypto.TagSize },
                _ => false
            };
            if (!ok)
                throw Corrupt(path, $"data key version {dk.Version} is malformed for {file.Protection} protection");
        }

        if (file.Protection == KeyProtection.None)
        {
            if (file.CurrentDataKeyVersion is not null || file.Kdf is not null)
                throw Corrupt(path, "pass-through mode cannot have a data key or KDF");
        }
        else if (file.CurrentDataKeyVersion is not { } current || !versions.Contains(current))
        {
            throw Corrupt(path, "the current data key version is missing");
        }

        if (file.Protection == KeyProtection.EnvironmentKek)
        {
            if (file.Kdf is not { Algorithm: KeyringCrypto.KdfAlgorithm, Iterations: > 0, Salt.Length: > 0 })
                throw Corrupt(path, "the KDF parameters are missing or unsupported");
        }
        else if (file.Kdf is not null)
        {
            throw Corrupt(path, "only environment protection has KDF parameters");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in file.Secrets)
        {
            if (!FileKeyringService.IsValidKeyName(s.Key))
                throw Corrupt(path, "a secret has an invalid name");
            if (!names.Add(s.Key))
                throw Corrupt(path, $"secret '{s.Key}' appears twice");

            var ok = file.Protection == KeyProtection.None
                ? s.Value is not null && s.DataKeyVersion is null && s.Ciphertext is null
                : s.Value is null && s.DataKeyVersion is { } v && versions.Contains(v)
                  && s.Nonce is { Length: KeyringCrypto.NonceSize } && s.Ciphertext is { Length: >= KeyringCrypto.TagSize };
            if (!ok)
                throw Corrupt(path, $"secret '{s.Key}' is malformed for {file.Protection} protection");
        }
    }

    /// <summary>Converts a validated file into state, using unwrapped data keys where needed.</summary>
    public static KeyringState ToState(KeyringFile file, IReadOnlyDictionary<int, byte[]>? unwrappedKeys = null)
    {
        var dataKeys = file.DataKeys.ToImmutableSortedDictionary(
            dk => dk.Version,
            dk => new DataKey(dk.Version, dk.Key ?? unwrappedKeys![dk.Version], dk.Nonce, dk.Wrapped));

        var secrets = file.Secrets.ToImmutableSortedDictionary(
            s => s.Key,
            s => new StoredSecret(s.Key, s.DataKeyVersion, s.Nonce, s.Ciphertext, s.Value, s.UpdatedAt),
            StringComparer.Ordinal);

        return new KeyringState(file.Protection, file.CurrentDataKeyVersion, dataKeys, secrets);
    }

    /// <summary>
    /// Writes the state atomically: a temp file (owner-only on Unix), flushed to disk, then renamed
    /// over the old file.
    /// </summary>
    /// <exception cref="IOException">The write failed; the previous file is untouched.</exception>
    /// <exception cref="UnauthorizedAccessException">The write failed; the previous file is untouched.</exception>
    public static void Save(string path, KeyringState state, EnvironmentKeyContext? env)
    {
        var file = new KeyringFile
        {
            Format = KeyringFile.CurrentFormat,
            Protection = state.Protection,
            CurrentDataKeyVersion = state.CurrentVersion,
            Kdf = state.Protection == KeyProtection.EnvironmentKek
                ? new KeyringKdf { Algorithm = env!.Kdf.Algorithm, Iterations = env.Kdf.Iterations, Salt = env.Kdf.Salt }
                : null,
            DataKeys = state.DataKeys.Values.Select(dk => state.Protection == KeyProtection.EnvironmentKek
                    ? new KeyringDataKey { Version = dk.Version, Nonce = dk.Nonce, Wrapped = dk.Wrapped }
                    : new KeyringDataKey { Version = dk.Version, Key = dk.Key })
                .ToList(),
            Secrets = state.Secrets.Values.Select(s => new KeyringSecret
                {
                    Key = s.Key, DataKeyVersion = s.DataKeyVersion, Nonce = s.Nonce,
                    Ciphertext = s.Ciphertext, Value = s.Value, UpdatedAt = s.UpdatedAt
                })
                .ToList()
        };

        var directory = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(directory))
        {
            if (OperatingSystem.IsWindows())
                Directory.CreateDirectory(directory);
            else
                Directory.CreateDirectory(directory, OwnerOnlyDirectory);
        }

        // Remove a leftover temp file so the owner-only mode applies (it is only set on creation).
        var temp = path + ".tmp";
        File.Delete(temp);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = OwnerOnlyFile;

        using (var stream = new FileStream(temp, options))
        {
            JsonSerializer.Serialize(stream, file, JsonOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    private static KeyringUnavailableException Corrupt(string path, string why, Exception? inner = null)
        => new(KeyringUnavailableReason.Corrupt,
            $"The keyring file '{path}' is corrupt: {why}. Restore it from a backup.", inner);
}
