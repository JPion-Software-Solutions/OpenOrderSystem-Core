using System.Security.Cryptography;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;
using OpenOrderSystem.Core.Services.Keyring.Model;

namespace OpenOrderSystem.Core.Services.Keyring;

/// <summary>What <see cref="KeyringBuilder.Build"/> did at boot. Flags may combine.</summary>
[Flags]
public enum KeyringBootActions
{
    None = 0,

    /// <summary>No keyring existed; a new one was created.</summary>
    Created = 1,

    /// <summary>Pass-through secrets were encrypted under a new data key.</summary>
    EncryptionEnabled = 2,

    /// <summary>The data key is now wrapped by the environment key.</summary>
    MovedToEnvironment = 4,

    /// <summary>The keyring was unlocked with <c>OOS_MASTERKEY_PREVIOUS</c> and re-wrapped with <c>OOS_MASTERKEY</c>.</summary>
    EnvironmentKeyReplaced = 8,

    /// <summary>The KEK was re-derived with stronger KDF parameters.</summary>
    KdfUpgraded = 16,

    /// <summary><c>OOS_MASTERKEY_RELEASE</c> moved the data key back to disk, unwrapped.</summary>
    ReleasedToDisk = 32,

    /// <summary><c>OOS_MASTERKEY_PREVIOUS</c> is set but was not needed; it can be removed.</summary>
    PreviousKeyStillSet = 64
}

/// <summary>Options for <see cref="KeyringBuilder.Build"/>. The defaults are for production.</summary>
public sealed record KeyringBuildOptions
{
    /// <summary>Reads environment variables. Replace in tests.</summary>
    public Func<string, string?> GetEnvironmentVariable { get; init; } = Environment.GetEnvironmentVariable;

    /// <summary>
    /// PBKDF2 iterations for new KEKs. A keyring stored with fewer is upgraded at boot; one stored
    /// with more is left alone.
    /// </summary>
    public int KdfIterations { get; init; } = KeyringCrypto.DefaultKdfIterations;

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

/// <param name="Keyring">The opened keyring.</param>
/// <param name="Actions">What changed at boot, for the boot screen and logs.</param>
public sealed record KeyringBuildResult(FileKeyringService Keyring, KeyringBootActions Actions);

/// <summary>
/// Opens the keyring at boot, before the DI container exists, and applies the changes that can
/// only happen at boot because they depend on environment variables.
/// </summary>
/// <remarks>
/// <para>Environment variables (empty counts as unset):</para>
/// <list type="bullet">
///   <item><c>OOS_MASTERKEY</c>: the environment key. When set, the keyring is moved to (or kept
///   in) environment protection.</item>
///   <item><c>OOS_MASTERKEY_PREVIOUS</c>: the old environment key, while replacing it.</item>
///   <item><c>OOS_MASTERKEY_RELEASE</c>: <c>true</c> or <c>1</c> to leave environment protection and
///   store the data key on disk, unwrapped. Needs the current <c>OOS_MASTERKEY</c> to unlock it.</item>
/// </list>
/// <para>What happens, by state on disk:</para>
/// <list type="table">
///   <listheader><term>On disk / environment</term><description>Result</description></listheader>
///   <item><term>Nothing</term><description>Create a keyring, then apply the rows below to it as pass-through.</description></item>
///   <item><term>Pass-through or on-disk key, no <c>OOS_MASTERKEY</c></term><description>Open as is.</description></item>
///   <item><term>On-disk key, <c>OOS_MASTERKEY</c> set</term><description>Wrap the data key; delete the unwrapped copy.</description></item>
///   <item><term>Pass-through, <c>OOS_MASTERKEY</c> set</term><description>Create a data key, encrypt every secret, wrap the key.</description></item>
///   <item><term>Environment, key matches</term><description>Open; re-wrap if the KDF parameters are out of date.</description></item>
///   <item><term>Environment, key missing</term><description>Throw <see cref="KeyringUnavailableReason.MasterKeyMissing"/>.</description></item>
///   <item><term>Environment, key does not match</term><description>Unlock with <c>OOS_MASTERKEY_PREVIOUS</c> and re-wrap with the new key; without a previous key throw <see cref="KeyringUnavailableReason.MasterKeyMismatch"/>, and if that does not match either, <see cref="KeyringUnavailableReason.PreviousKeyMismatch"/>.</description></item>
///   <item><term>Environment, <c>OOS_MASTERKEY_RELEASE</c> set</term><description>Unlock, then store the data key unwrapped.</description></item>
/// </list>
/// <para>
/// Every change is one atomic write of the keyring file, so a crash during boot leaves either the
/// old keyring or the new one, and the next boot finishes the job. Failures throw
/// <see cref="KeyringUnavailableException"/>; boot should then fall back to recovery.
/// </para>
/// </remarks>
public static class KeyringBuilder
{
    public const string MasterKeyVariable = "OOS_MASTERKEY";
    public const string PreviousMasterKeyVariable = "OOS_MASTERKEY_PREVIOUS";
    public const string ReleaseVariable = "OOS_MASTERKEY_RELEASE";
    public const string KeyringFileName = "keyring.json";

    /// <summary>Opens (or creates) the keyring in <paramref name="protectedRoot"/>.</summary>
    /// <exception cref="KeyringUnavailableException">The keyring cannot be opened. See <see cref="KeyringUnavailableException.Reason"/>.</exception>
    public static KeyringBuildResult Build(string protectedRoot, KeyringBuildOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedRoot);
        options ??= new KeyringBuildOptions();
        if (options.KdfIterations < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "KdfIterations must be positive.");

        var path = Path.Combine(Path.GetFullPath(protectedRoot), KeyringFileName);
        var masterKey = Read(options, MasterKeyVariable);
        var previousKey = Read(options, PreviousMasterKeyVariable);
        var release = Read(options, ReleaseVariable) is { } r
                      && (r.Equals("true", StringComparison.OrdinalIgnoreCase) || r == "1");

        var actions = KeyringBootActions.None;
        var changed = false;
        var file = KeyringStore.Load(path);

        // ---- 1. Open what is on disk.
        KeyringState state;
        byte[]? kek = null;              // KEK that currently wraps the data keys, if still valid
        KdfParameters? kdf = null;

        if (file is null)
        {
            state = KeyringState.Empty;
            actions |= KeyringBootActions.Created;
            changed = true;
        }
        else if (file.Protection != KeyProtection.EnvironmentKek)
        {
            state = KeyringStore.ToState(file);
        }
        else
        {
            if (masterKey is null)
                throw new KeyringUnavailableException(KeyringUnavailableReason.MasterKeyMissing,
                    $"The keyring is protected by an environment key, but {MasterKeyVariable} is not set.");

            kdf = new KdfParameters(file.Kdf!.Algorithm, file.Kdf.Iterations, file.Kdf.Salt);
            var candidate = KeyringCrypto.DeriveKek(masterKey, kdf.Salt, kdf.Iterations);

            if (TryUnwrap(file, candidate, out var dataKeys))
            {
                kek = candidate;
                if (previousKey is not null)
                    actions |= KeyringBootActions.PreviousKeyStillSet;
            }
            else
            {
                CryptographicOperations.ZeroMemory(candidate);
                if (previousKey is null)
                    throw new KeyringUnavailableException(KeyringUnavailableReason.MasterKeyMismatch,
                        $"{MasterKeyVariable} does not unlock the keyring. If you are replacing the key, set " +
                        $"{PreviousMasterKeyVariable} to the old one.");

                var previousKek = KeyringCrypto.DeriveKek(previousKey, kdf.Salt, kdf.Iterations);
                var unlocked = TryUnwrap(file, previousKek, out dataKeys);
                CryptographicOperations.ZeroMemory(previousKek);
                if (!unlocked)
                    throw new KeyringUnavailableException(KeyringUnavailableReason.PreviousKeyMismatch,
                        $"Neither {MasterKeyVariable} nor {PreviousMasterKeyVariable} unlocks the keyring.");

                actions |= KeyringBootActions.EnvironmentKeyReplaced;
            }

            state = KeyringStore.ToState(file, dataKeys);
        }

        // ---- 2. Decide where it should end up.
        var target = release
            ? (state.Protection == KeyProtection.EnvironmentKek ? KeyProtection.DataKeyOnDisk : state.Protection)
            : masterKey is not null ? KeyProtection.EnvironmentKek : state.Protection;

        // ---- 3. Move it there.
        EnvironmentKeyContext? environment = null;

        if (target == KeyProtection.EnvironmentKek)
        {
            var outdated = kdf is null
                           || kdf.Algorithm != KeyringCrypto.KdfAlgorithm
                           || kdf.Iterations < options.KdfIterations;

            if (kek is null || outdated)
            {
                if (state.Protection == KeyProtection.EnvironmentKek && kek is not null)
                    actions |= KeyringBootActions.KdfUpgraded;
                else if (state.Protection != KeyProtection.EnvironmentKek)
                    actions |= KeyringBootActions.MovedToEnvironment;

                if (kek is not null)
                    CryptographicOperations.ZeroMemory(kek);

                kdf = new KdfParameters(KeyringCrypto.KdfAlgorithm,
                    Math.Max(options.KdfIterations, kdf?.Iterations ?? 0), KeyringCrypto.NewSalt());
                kek = KeyringCrypto.DeriveKek(masterKey!, kdf.Salt, kdf.Iterations);

                if (state.Protection == KeyProtection.None)
                {
                    state = FileKeyringService.EncryptPassThrough(state, KeyProtection.EnvironmentKek, kek);
                    if (state.Secrets.Count > 0)
                        actions |= KeyringBootActions.EncryptionEnabled;
                }
                else
                {
                    state = state with
                    {
                        Protection = KeyProtection.EnvironmentKek,
                        DataKeys = state.DataKeys.SetItems(state.DataKeys.Values.Select(dk =>
                            KeyValuePair.Create(dk.Version, FileKeyringService.Wrap(dk, kek))))
                    };
                }

                changed = true;
            }

            environment = new EnvironmentKeyContext(kek, kdf!, KeyringCrypto.Fingerprint(kek),
                KeyringCrypto.LooksWeak(masterKey!));
        }
        else if (state.Protection == KeyProtection.EnvironmentKek)
        {
            // Release: keep the data keys, drop the wrapping. (kek is null if the previous key unlocked it.)
            if (kek is not null)
                CryptographicOperations.ZeroMemory(kek);
            state = state with
            {
                Protection = KeyProtection.DataKeyOnDisk,
                DataKeys = state.DataKeys.SetItems(state.DataKeys.Values.Select(dk =>
                    KeyValuePair.Create(dk.Version, dk with { Nonce = null, Wrapped = null })))
            };
            actions |= KeyringBootActions.ReleasedToDisk;
            changed = true;
        }

        // ---- 4. Persist once.
        if (changed)
        {
            try
            {
                KeyringStore.Save(path, state, environment);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new KeyringUnavailableException(KeyringUnavailableReason.StorageError,
                    $"The keyring file '{path}' could not be written: {ex.Message}", ex);
            }
        }

        return new KeyringBuildResult(new FileKeyringService(path, state, environment, options.TimeProvider), actions);
    }

    private static string? Read(KeyringBuildOptions options, string name)
        => options.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>Unwraps every data key, or returns false if any fails (wrong KEK or tampering).</summary>
    private static bool TryUnwrap(KeyringFile file, byte[] kek, out Dictionary<int, byte[]> keys)
    {
        keys = new Dictionary<int, byte[]>();
        try
        {
            foreach (var dk in file.DataKeys)
                keys[dk.Version] = KeyringCrypto.Open(kek, dk.Nonce, dk.Wrapped, KeyringCrypto.DataKeyAad(dk.Version));
            return true;
        }
        catch (CryptographicException)
        {
            foreach (var key in keys.Values)
                CryptographicOperations.ZeroMemory(key);
            keys.Clear();
            return false;
        }
    }
}
