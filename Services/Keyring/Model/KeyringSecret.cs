namespace OpenOrderSystem.Core.Services.Keyring.Model;

/// <summary>
/// A secret as stored in the keyring file. Either <see cref="Value"/> is set (pass-through mode)
/// or <see cref="DataKeyVersion"/>, <see cref="Nonce"/> and <see cref="Ciphertext"/> are.
/// </summary>
internal sealed class KeyringSecret
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Data key the value is encrypted with; null when stored as plain text.</summary>
    public int? DataKeyVersion { get; set; }

    /// <summary>AES-GCM nonce (12 bytes).</summary>
    public byte[]? Nonce { get; set; }

    /// <summary>AES-GCM ciphertext followed by the 16-byte tag.</summary>
    public byte[]? Ciphertext { get; set; }

    /// <summary>The plain-text value, only in pass-through mode.</summary>
    public string? Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
