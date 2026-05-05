// OpenOrderSystem.Core/Extensions/AspNet/ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace OpenOrderSystem.Core.Extensions.AspNet;

/// <summary>
/// General-purpose extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a keyed service with the specified lifetime without requiring a separate
    /// call per lifetime variant.
    /// </summary>
    /// <typeparam name="TService">The service type to register.</typeparam>
    /// <param name="bob">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="serviceKey">The key to associate with this registration.</param>
    /// <param name="implementationType">The concrete implementation type.</param>
    /// <param name="lifetime">The desired service lifetime.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKeyedService<TService>(
        this IServiceCollection bob,
        object serviceKey,
        Type implementationType,
        ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                bob.AddKeyedScoped(typeof(TService), serviceKey, implementationType);
                break;
            case ServiceLifetime.Singleton:
                bob.AddKeyedSingleton(typeof(TService), serviceKey, implementationType);
                break;
            case ServiceLifetime.Transient:
                bob.AddKeyedTransient(typeof(TService), serviceKey, implementationType);
                break;
        }
        return bob;
    }
    
    /// <summary>
    /// Attempts to register a service with the specified lifetime without requiring a separate
    /// call per lifetime variant. Does nothing if a registration for <paramref name="serviceType"/>
    /// already exists.
    /// </summary>
    /// <param name="bob">The <see cref="IServiceCollection"/> to register into.</param>
    /// <param name="serviceType">The service type to register.</param>
    /// <param name="lifetime">The desired service lifetime.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection TryAddService(
        this IServiceCollection bob,
        Type serviceType,
        ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Scoped:
                bob.TryAddScoped(serviceType);
                break;
            case ServiceLifetime.Singleton:
                bob.TryAddSingleton(serviceType);
                break;
            case ServiceLifetime.Transient:
                bob.TryAddTransient(serviceType);
                break;
        }
        return bob;
    }
}