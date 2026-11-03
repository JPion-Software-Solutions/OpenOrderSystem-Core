using OpenOrderSystem.Core.Services.Keyring.Interfaces;

namespace OpenOrderSystem.Core.Services.Keyring.Model;

/// <summary>
/// Root of <c>keyring.json</c> in the protected root. Everything lives in one file so every
/// change, including a rotation or a change of protection, is a single atomic write.
/// </summary>
internal sealed class KeyringFile
{
    public const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;
    public KeyProtection Protection { get; set; }

    /// <summary>How the KEK is derived from the environment key; only under environment protection.</summary>
    public KeyringKdf? Kdf { get; set; }

    /// <summary>Version new values are encrypted with; null in pass-through mode.</summary>
    public int? CurrentDataKeyVersion { get; set; }

    public List<KeyringDataKey> DataKeys { get; set; } = [];
    public List<KeyringSecret> Secrets { get; set; } = [];
}

/// <summary>KEK derivation parameters.</summary>
internal sealed class KeyringKdf
{
    public string Algorithm { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public byte[] Salt { get; set; } = [];
}

/// <summary>
/// A data key. <see cref="Key"/> is set when stored unwrapped (<see cref="KeyProtection.DataKeyOnDisk"/>);
/// <see cref="Nonce"/> and <see cref="Wrapped"/> when wrapped by the KEK (<see cref="KeyProtection.EnvironmentKek"/>).
/// </summary>
internal sealed class KeyringDataKey
{
    public int Version { get; set; }
    public byte[]? Key { get; set; }
    public byte[]? Nonce { get; set; }
    public byte[]? Wrapped { get; set; }
}
