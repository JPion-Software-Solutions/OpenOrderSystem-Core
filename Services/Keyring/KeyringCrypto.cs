using System.Security.Cryptography;
using System.Text;

namespace OpenOrderSystem.Core.Services.Keyring;

/// <summary>
/// Cryptographic primitives for the keyring. AES-256-GCM for secrets and for wrapping data keys;
/// PBKDF2-HMAC-SHA256 to derive the KEK from the environment key.
/// </summary>
internal static class KeyringCrypto
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int SaltSize = 16;
    public const string KdfAlgorithm = "PBKDF2-SHA256";

    /// <summary>OWASP's 2023 recommendation for PBKDF2-HMAC-SHA256.</summary>
    public const int DefaultKdfIterations = 600_000;

    public static byte[] NewKey() => RandomNumberGenerator.GetBytes(KeySize);
    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>
    /// Associated data binding a secret's ciphertext to its name and data key version, so
    /// ciphertext copied between secrets or relabelled to another version fails to decrypt.
    /// </summary>
    public static string SecretAad(string key, int version) => $"oos-keyring/v1/secret/{version}/{key}";

    /// <summary>Associated data binding a wrapped data key to its version.</summary>
    public static string DataKeyAad(int version) => $"oos-keyring/v1/dek/{version}";

    public static (byte[] Nonce, byte[] Ciphertext) Seal(byte[] key, ReadOnlySpan<byte> plaintext, string aad)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var output = new byte[plaintext.Length + TagSize];
        using var gcm = new AesGcm(key, TagSize);
        gcm.Encrypt(nonce, plaintext, output.AsSpan(0, plaintext.Length), output.AsSpan(plaintext.Length),
            Encoding.UTF8.GetBytes(aad));
        return (nonce, output);
    }

    /// <exception cref="CryptographicException">Wrong key, tampered data, or malformed input.</exception>
    public static byte[] Open(byte[] key, byte[]? nonce, byte[]? sealedData, string aad)
    {
        if (nonce is not { Length: NonceSize } || sealedData is null || sealedData.Length < TagSize)
            throw new CryptographicException("Malformed sealed data.");

        var plaintextLength = sealedData.Length - TagSize;
        var plaintext = new byte[plaintextLength];
        using var gcm = new AesGcm(key, TagSize);
        gcm.Decrypt(nonce, sealedData.AsSpan(0, plaintextLength), sealedData.AsSpan(plaintextLength), plaintext,
            Encoding.UTF8.GetBytes(aad));
        return plaintext;
    }

    /// <summary>
    /// Derives the KEK. Surrounding whitespace in the environment key is ignored, so a trailing
    /// newline from a secrets file does not change the key.
    /// </summary>
    public static byte[] DeriveKek(string environmentKey, byte[] salt, int iterations)
    {
        var password = Encoding.UTF8.GetBytes(environmentKey.Trim());
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }

    /// <summary>First 8 hex characters of SHA-256(KEK).</summary>
    public static string Fingerprint(byte[] kek) => Convert.ToHexStringLower(SHA256.HashData(kek))[..8];

    /// <summary>
    /// True when the environment key looks like a typed passphrase rather than a generated key.
    /// Strong means it decodes from hex or base64 to at least 32 bytes, or is at least 64
    /// characters long.
    /// </summary>
    public static bool LooksWeak(string environmentKey)
    {
        var key = environmentKey.Trim();
        if (key.Length >= 64)
            return false;

        if (key.Length % 2 == 0 && key.All(char.IsAsciiHexDigit) && key.Length / 2 >= KeySize)
            return false;

        var base64 = key.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        var buffer = new byte[base64.Length];
        if (Convert.TryFromBase64String(base64, buffer, out var written) && written >= KeySize)
            return false;

        return true;
    }
}
