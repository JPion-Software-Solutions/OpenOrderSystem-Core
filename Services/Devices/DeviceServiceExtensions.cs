// OpenOrderSystem.Core/Services/Devices/DeviceServiceExtensions.cs
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Extensions.AspNet;
using OpenOrderSystem.Core.Services.Devices.Attributes;
using OpenOrderSystem.Core.Services.Devices.Interfaces;

namespace OpenOrderSystem.Core.Services.Devices;

/// <summary>
/// Extension methods for registering OOS device services and handlers.
/// </summary>
public static class DeviceServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IDeviceService"/> and scans the provided assemblies for
    /// <see cref="IDeviceHandler"/> implementations decorated with <see cref="DeviceHandlerInfoAttribute"/>,
    /// registering each as a keyed service using its declared device type as the key.
    /// </summary>
    /// <param name="bob">The <see cref="IServiceCollection"/> to register services into.</param>
    /// <param name="assemblies">
    /// One or more assemblies to scan for handler implementations. Typically the core assembly
    /// and any loaded plugin assemblies.
    /// </param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Boot-time validation queries the database for any <see cref="Data.DataModels.V2.Devices.DeviceHead"/>
    /// rows whose <c>DeviceType</c> has no registered handler and logs a warning for each.
    /// </para>
    /// <para>
    /// Call this method once per host build, passing all relevant assemblies:
    /// <code>
    /// builder.Services.AddDeviceHandlers(typeof(Program).Assembly);
    /// builder.Services.AddDeviceHandlers(pluginAssembly);
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddDeviceHandlers(
        this IServiceCollection bob,
        params Assembly[] assemblies)
    {
        bob.AddScoped<IDeviceService, DeviceService>();

        var handlerTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.IsAssignableTo(typeof(IDeviceHandler))
                        && t.GetCustomAttribute<DeviceHandlerInfoAttribute>() is not null);

        foreach (var handlerType in handlerTypes)
        {
            var info = handlerType.GetCustomAttribute<DeviceHandlerInfoAttribute>()!;

            bob.AddKeyedService<IDeviceHandler>(info.DeviceType, handlerType, info.Lifetime);

            foreach (var dependency in info.Dependencies)
                bob.TryAddService(dependency.Type, info.Lifetime);
            
            foreach (var dependency in handlerType.GetCustomAttributes<HandlerDependencyAttribute>())
                bob.TryAddService(dependency.Type, dependency.Lifetime);
            
        }

        return bob;
    }

    /// <summary>
    /// Validates that all device types present in the database have a registered handler.
    /// Logs a warning for each orphaned device type. Should be called after the host is built
    /// but before it starts accepting requests.
    /// </summary>
    /// <param name="app">The built <see cref="IServiceProvider"/>.</param>
    /// <returns>A task representing the asynchronous validation operation.</returns>
    public static async Task ValidateDeviceHandlersAsync(this IServiceProvider app)
    {
        using var scope = app.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILogger<DeviceService>>();
        var contextFactory = sp.GetRequiredService<IDbContextFactory<OosDbContext>>();

        await using var db = await contextFactory.CreateDbContextAsync();

        var deviceTypes = await db.Devices
            .AsNoTracking()
            .Select(d => d.DeviceType)
            .Distinct()
            .ToListAsync();

        foreach (var deviceType in deviceTypes)
        {
            var handler = sp.GetKeyedService<IDeviceHandler>(deviceType);
            if (handler is null)
            {
                logger.LogWarning(
                    "Device validation: no handler registered for device type '{DeviceType}'. " +
                    "Devices of this type will not respond to commands.",
                    deviceType);
            }
        }
    }
}