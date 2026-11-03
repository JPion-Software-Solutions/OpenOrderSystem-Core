namespace OpenOrderSystem.Core.Services.Keyring.Interfaces;

/// <summary>Why the keyring could not be opened at boot.</summary>
public enum KeyringUnavailableReason
{
    /// <summary>The keyring is environment-protected but <c>OOS_MASTERKEY</c> is not set.</summary>
    MasterKeyMissing,

    /// <summary><c>OOS_MASTERKEY</c> does not unlock the keyring and no previous key was supplied.</summary>
    MasterKeyMismatch,

    /// <summary>
    /// Neither <c>OOS_MASTERKEY</c> nor <c>OOS_MASTERKEY_PREVIOUS</c> unlocks the keyring.
    /// </summary>
    PreviousKeyMismatch,

    /// <summary>The keyring file exists but is malformed or internally inconsistent.</summary>
    Corrupt,

    /// <summary>The keyring file was written by a newer version of OOS.</summary>
    UnsupportedFormat,

    /// <summary>The keyring file or protected root could not be read or written.</summary>
    StorageError
}

/// <summary>
/// Thrown by <c>KeyringBuilder</c> when the keyring cannot be opened. Boot should not continue in
/// normal mode; preflight can catch this and fall back to recovery. The message never contains
/// key material or secret values.
/// </summary>
public sealed class KeyringUnavailableException : Exception
{
    public KeyringUnavailableReason Reason { get; }

    public KeyringUnavailableException(KeyringUnavailableReason reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }
}

/// <summary>
/// A keyring write could not be saved to disk. The change did not take effect, in memory or on
/// disk; the caller may retry.
/// </summary>
public sealed class KeyringStorageException(string path, Exception inner)
    : IOException($"The keyring could not be saved to '{path}'. The change was not applied.", inner);
