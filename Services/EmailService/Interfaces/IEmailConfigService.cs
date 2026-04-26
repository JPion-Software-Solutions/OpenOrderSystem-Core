namespace OpenOrderSystem.Core.Services.EmailService.Interfaces;

/// <summary>
/// Defines a generic key-value configuration store for email service implementations.
/// Each implementation of <see cref="IEmailService"/> is responsible for defining
/// its own well-known keys and interpreting the values it retrieves.
/// The type parameter scopes each operation to the calling service, allowing the
/// implementation to namespace keys, log access, or enforce isolation between services.
/// </summary>
public interface IEmailConfigService
{
    /// <summary>
    /// Retrieves a configuration value scoped to the specified caller type.
    /// </summary>
    /// <typeparam name="T">The type of the calling service. Used to scope the key lookup.</typeparam>
    /// <param name="key">The configuration key to retrieve.</param>
    /// <returns>The configuration value, or <c>null</c> if the key is not found.</returns>
    string? GetKey<T>(string key);

    /// <summary>
    /// Stores a configuration value scoped to the specified caller type.
    /// </summary>
    /// <typeparam name="T">The type of the calling service. Used to scope the key.</typeparam>
    /// <param name="key">The configuration key to set.</param>
    /// <param name="value">The value to store.</param>
    void SetKey<T>(string key, string value);
}