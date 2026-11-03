using System.Security.Cryptography;
using System.Text.Json;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;
using OpenOrderSystem.Core.Services.Keyring.Model;

namespace OpenOrderSystem.Core.Services.Keyring;

/// <summary>What state the keyring file is in, as far as the inspector could tell.</summary>
public enum KeyringFileCondition
{
    /// <summary>No keyring file exists.</summary>
    Missing,

    /// <summary>The file passed full validation. The inventory is complete.</summary>
    Valid,

    /// <summary>
    /// The file is malformed, truncated or inconsistent. The inventory holds whatever names could be
    /// salvaged and may be incomplete.
    /// </summary>
    Corrupt,

    /// <summary>
    /// The file was written by a newer OOS. Names were read on a best-effort basis and may be incomplete.
    /// </summary>
    UnsupportedFormat,

    /// <summary>The file exists but could not be read (permissions or I/O). Nothing is known about it.</summary>
    Unreadable
}

/// <summary>Result of checking the environment key against the keyring, without changing anything.</summary>
public enum MasterKeyCheck
{
    /// <summary>The check was not requested (see <see cref="KeyringInspectOptions.CheckMasterKey"/>).</summary>
    NotChecked,

    /// <summary>The keyring is not environment-protected, or the file could not be validated.</summary>
    NotApplicable,

    /// <summary>The keyring is environment-protected and <c>OOS_MASTERKEY</c> is not set.</summary>
    NotSet,

    /// <summary><c>OOS_MASTERKEY</c> unlocks the keyring.</summary>
    Unlocks,

    /// <summary><c>OOS_MASTERKEY</c> does not unlock it, but <c>OOS_MASTERKEY_PREVIOUS</c> does.</summary>
    OnlyPreviousUnlocks,

    /// <summary>Neither key unlocks the keyring (or only <c>OOS_MASTERKEY</c> is set and it does not).</summary>
    NeitherUnlocks
}

/// <summary>A secret as listed by the inspector. Never carries a value.</summary>
/// <param name="Key">The secret's name.</param>
/// <param name="DataKeyVersion">The data key version it is encrypted with; null if stored as plain text or unknown.</param>
/// <param name="UpdatedAt">When it was last written; null if the file did not say (salvaged entries only).</param>
public sealed record KeyringSecretEntry(string Key, int? DataKeyVersion, DateTimeOffset? UpdatedAt);

/// <summary>A value-free description of the keyring file, for recovery and diagnostics.</summary>
/// <param name="Path">Full path of the keyring file.</param>
/// <param name="Condition">What state the file is in.</param>
/// <param name="Problem">Why the file is not <see cref="KeyringFileCondition.Valid"/>; null when it is valid or missing.</param>
/// <param name="Format">File format number, if it could be read.</param>
/// <param name="Protection">Protection mode, if it could be read.</param>
/// <param name="CurrentDataKeyVersion">Current data key version; only for a valid file.</param>
/// <param name="DataKeyVersions">Data key versions present; only for a valid file.</param>
/// <param name="Secrets">Secret names and metadata, sorted by name. Never values.</param>
/// <param name="SkippedEntries">
/// Secret entries that were found but could not be listed (no name, an invalid name, or a duplicate).
/// Always 0 for a valid file.
/// </param>
/// <param name="MasterKey">Result of the optional environment key check.</param>
public sealed record KeyringInventory(
    string Path,
    KeyringFileCondition Condition,
    string? Problem,
    int? Format,
    KeyProtection? Protection,
    int? CurrentDataKeyVersion,
    IReadOnlyList<int> DataKeyVersions,
    IReadOnlyList<KeyringSecretEntry> Secrets,
    int SkippedEntries,
    MasterKeyCheck MasterKey)
{
    /// <summary>True when <see cref="Secrets"/> is known to be the full list.</summary>
    public bool IsComplete => Condition is KeyringFileCondition.Valid or KeyringFileCondition.Missing;
}

/// <summary>Options for <see cref="KeyringInspector.Inspect"/>.</summary>
public sealed record KeyringInspectOptions
{
    /// <summary>Reads environment variables. Replace in tests.</summary>
    public Func<string, string?> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>
    /// Also test <c>OOS_MASTERKEY</c> (and <c>OOS_MASTERKEY_PREVIOUS</c>) against an environment-protected
    /// keyring. Off by default: it runs the KDF, which is deliberately slow.
    /// </summary>
    public bool CheckMasterKey { get; init; }
}

/// <summary>
/// Reads the keyring file without opening it: no master key needed, nothing decrypted, nothing written.
/// For boot modes that must run without a working keyring, such as recovery.
/// </summary>
/// <remarks>
/// <para>
/// Secret names are stored in plain text, so they can always be listed. When the file fails validation,
/// the inspector falls back to a token-by-token read that keeps every secret name it reaches before
/// the damage, so a truncated or partly malformed file still yields a list to rebuild from.
/// </para>
/// <para>
/// The inventory never contains secret values, data keys or ciphertext, even for a pass-through keyring
/// whose values are stored in plain text.
/// </para>
/// </remarks>
public static class KeyringInspector
{
    /// <summary>Describes the keyring in <paramref name="protectedRoot"/>. Never throws for a bad keyring file.</summary>
    public static KeyringInventory Inspect(string protectedRoot, KeyringInspectOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedRoot);
        options ??= new KeyringInspectOptions();

        var path = Path.Combine(Path.GetFullPath(protectedRoot), KeyringBuilder.KeyringFileName);

        KeyringFile? file;
        try
        {
            file = KeyringStore.Load(path);
        }
        catch (KeyringUnavailableException ex) when (ex.Reason == KeyringUnavailableReason.StorageError)
        {
            return Empty(path, KeyringFileCondition.Unreadable, ex.Message, options);
        }
        catch (KeyringUnavailableException ex)
        {
            var condition = ex.Reason == KeyringUnavailableReason.UnsupportedFormat
                ? KeyringFileCondition.UnsupportedFormat
                : KeyringFileCondition.Corrupt;
            return Salvage(path, condition, ex.Message, options);
        }

        if (file is null)
            return Empty(path, KeyringFileCondition.Missing, null, options);

        var secrets = file.Secrets
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => new KeyringSecretEntry(s.Key, s.DataKeyVersion, s.UpdatedAt))
            .ToList();

        return new KeyringInventory(
            path,
            KeyringFileCondition.Valid,
            null,
            file.Format,
            file.Protection,
            file.CurrentDataKeyVersion,
            file.DataKeys.Select(dk => dk.Version).Order().ToList(),
            secrets,
            0,
            CheckMasterKey(file, options));
    }

    private static KeyringInventory Empty(string path, KeyringFileCondition condition, string? problem,
        KeyringInspectOptions options)
        => new(path, condition, problem, null, null, null, [], [], 0,
            options.CheckMasterKey ? MasterKeyCheck.NotApplicable : MasterKeyCheck.NotChecked);

    // ---- Master key check

    private static MasterKeyCheck CheckMasterKey(KeyringFile file, KeyringInspectOptions options)
    {
        if (!options.CheckMasterKey)
            return MasterKeyCheck.NotChecked;
        if (file.Protection != KeyProtection.EnvironmentKek)
            return MasterKeyCheck.NotApplicable;

        var master = Read(options, KeyringBuilder.MasterKeyVariable);
        if (master is null)
            return MasterKeyCheck.NotSet;
        if (Unlocks(file, master))
            return MasterKeyCheck.Unlocks;

        var previous = Read(options, KeyringBuilder.PreviousMasterKeyVariable);
        return previous is not null && Unlocks(file, previous)
            ? MasterKeyCheck.OnlyPreviousUnlocks
            : MasterKeyCheck.NeitherUnlocks;
    }

    private static bool Unlocks(KeyringFile file, string environmentKey)
    {
        var kek = KeyringCrypto.DeriveKek(environmentKey, file.Kdf!.Salt, file.Kdf.Iterations);
        try
        {
            if (!KeyringBuilder.TryUnwrap(file, kek, out var keys))
                return false;
            foreach (var key in keys.Values)
                CryptographicOperations.ZeroMemory(key);
            return true;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static string? Read(KeyringInspectOptions options, string name)
        => options.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    // ---- Salvage

    /// <summary>
    /// Walks the raw JSON token by token and keeps every secret name it reaches. Stops quietly at the
    /// first token it cannot read, so a truncated file still yields the names before the cut.
    /// </summary>
    private static KeyringInventory Salvage(string path, KeyringFileCondition condition, string problem,
        KeyringInspectOptions options)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Empty(path, KeyringFileCondition.Unreadable, ex.Message, options);
        }

        int? format = null;
        KeyProtection? protection = null;
        var found = new Dictionary<string, KeyringSecretEntry>(StringComparer.Ordinal);
        var skipped = 0;

        string? topProperty = null;   // property of the root object we are inside
        string? secretProperty = null; // property of the current secret object
        string? name = null;
        int? version = null;
        DateTimeOffset? updatedAt = null;
        var inSecret = false;

        void Commit()
        {
            if (name is null || !FileKeyringService.IsValidKeyName(name))
            {
                skipped++;
            }
            else if (found.TryGetValue(name, out var existing))
            {
                skipped++;
                if ((updatedAt ?? DateTimeOffset.MinValue) > (existing.UpdatedAt ?? DateTimeOffset.MinValue))
                    found[name] = new KeyringSecretEntry(name, version, updatedAt);
            }
            else
            {
                found[name] = new KeyringSecretEntry(name, version, updatedAt);
            }

            name = null;
            version = null;
            updatedAt = null;
            inSecret = false;
        }

        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName when reader.CurrentDepth == 1:
                        topProperty = reader.GetString();
                        break;

                    case JsonTokenType.String when reader.CurrentDepth == 1 && Is(topProperty, "protection"):
                        if (Enum.TryParse<KeyProtection>(reader.GetString(), true, out var p) && Enum.IsDefined(p))
                            protection = p;
                        break;

                    case JsonTokenType.Number when reader.CurrentDepth == 1 && Is(topProperty, "format"):
                        if (reader.TryGetInt32(out var f))
                            format = f;
                        break;

                    case JsonTokenType.StartObject when reader.CurrentDepth == 2 && Is(topProperty, "secrets"):
                        inSecret = true;
                        break;

                    case JsonTokenType.EndObject when reader.CurrentDepth == 2 && inSecret:
                        Commit();
                        break;

                    case JsonTokenType.PropertyName when reader.CurrentDepth == 3 && inSecret:
                        secretProperty = reader.GetString();
                        break;

                    // Only the name and metadata are read; "value", "nonce" and "ciphertext" are never touched.
                    case JsonTokenType.String when reader.CurrentDepth == 3 && inSecret && Is(secretProperty, "key"):
                        name = reader.GetString();
                        break;

                    case JsonTokenType.String when reader.CurrentDepth == 3 && inSecret && Is(secretProperty, "updatedAt"):
                        if (DateTimeOffset.TryParse(reader.GetString(), out var at))
                            updatedAt = at;
                        break;

                    case JsonTokenType.Number when reader.CurrentDepth == 3 && inSecret && Is(secretProperty, "dataKeyVersion"):
                        if (reader.TryGetInt32(out var v))
                            version = v;
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Damaged from here on; keep what was read so far.
        }

        // A secret cut off mid-object still counts if its name was reached.
        if (inSecret)
            Commit();

        var secrets = found.Values.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();
        return new KeyringInventory(path, condition, problem, format, protection, null, [], secrets, skipped,
            options.CheckMasterKey ? MasterKeyCheck.NotApplicable : MasterKeyCheck.NotChecked);
    }

    private static bool Is(string? property, string expected)
        => string.Equals(property, expected, StringComparison.OrdinalIgnoreCase);
}
