namespace OpenOrderSystem.Core.Services.Keyring.Interfaces;

/// <summary>
/// Read-only access to the OOS keyring. Built by <c>KeyringBuilder</c> before the DI
/// container exists, then registered as a singleton for the rest of the process lifetime.
/// </summary>
/// <remarks>
/// Values returned by this interface are plaintext. Callers must use them immediately
/// and must never cache, log, or persist them. Secret names (not values) are safe to
/// display to admins.
/// </remarks>
public interface IReadOnlyKeyring
{
    /// <summary>
    /// The token format recognized by <see cref="ExpandSecrets"/>: <c>&lt;&lt;key:name&gt;&gt;</c>.
    /// Escaping is not supported; literal token text cannot appear in an expanded template.
    /// </summary>
    const string TokenPrefix = "<<key:";
    const string TokenSuffix = ">>";

    /// <summary>
    /// Returns the plaintext value of the named secret.
    /// </summary>
    /// <exception cref="MissingSecretException">The secret does not exist.</exception>
    string GetSecret(string key);

    /// <summary>
    /// Attempts to return the plaintext value of the named secret.
    /// Use this where a missing secret is an expected state, such as an
    /// unprovisioned plugin.
    /// </summary>
    bool TryGetSecret(string key, out string value);

    /// <summary>
    /// Returns whether the named secret exists, without decrypting it.
    /// </summary>
    bool HasSecret(string key);

    /// <summary>
    /// Returns the names of all stored secrets. Never returns values.
    /// </summary>
    IReadOnlyCollection<string> GetSecretNames();

    /// <summary>
    /// Returns <paramref name="template"/> with every <c>&lt;&lt;key:name&gt;&gt;</c> token
    /// replaced by the named secret's plaintext value. A template with no tokens is
    /// returned unchanged.
    /// </summary>
    /// <exception cref="MissingSecretException">
    /// One or more referenced secrets do not exist. All missing names are reported at once.
    /// </exception>
    string ExpandSecrets(string template)
    {
        if (string.IsNullOrEmpty(template) || !template.Contains(TokenPrefix))
            return template;

        var result = new System.Text.StringBuilder(template.Length);
        List<string>? missing = null;
        var index = 0;

        while (index < template.Length)
        {
            var start = template.IndexOf(TokenPrefix, index, StringComparison.Ordinal);
            if (start < 0)
                break;

            var nameStart = start + TokenPrefix.Length;
            var end = template.IndexOf(TokenSuffix, nameStart, StringComparison.Ordinal);
            if (end < 0)
                break; // unterminated token: left as literal text

            result.Append(template, index, start - index);
            var name = template[nameStart..end];

            if (TryGetSecret(name, out var value))
                result.Append(value);
            else
                (missing ??= []).Add(name);

            index = end + TokenSuffix.Length;
        }

        if (missing is not null)
            throw new MissingSecretException(missing.Distinct().ToList());

        result.Append(template, index, template.Length - index);
        return result.ToString();
    }
}

/// <summary>
/// Thrown when one or more requested secrets are not in the keyring.
/// The message lists secret names only, never values.
/// </summary>
public sealed class MissingSecretException : Exception
{
    public IReadOnlyList<string> MissingKeys { get; }

    public MissingSecretException(IReadOnlyList<string> missingKeys)
        : base($"Missing keyring secret(s): {string.Join(", ", missingKeys)}")
    {
        MissingKeys = missingKeys;
    }

    public MissingSecretException(string missingKey)
        : this([missingKey]) { }
}