using System.Reflection;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.API.Device;

public static class __Extensions
{
    private static readonly MethodInfo MapDeviceTypeMethod =
        typeof(__Extensions).GetMethod(nameof(MapDeviceType), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Maps the device API. Every device type in the <see cref="IDeviceTypeCatalog"/> gets its own
    /// <c>/device/v1/{key}</c> group, filled by its <see cref="IDeviceTypeDefinition.MapEndpoints"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <see cref="DevicesBuilderExtensions.AddOosDeviceServices"/> has not registered the
    /// <see cref="IDeviceTypeCatalog"/>.
    /// </exception>
    public static WebApplication MapDeviceApiRoutes(this WebApplication app)
    {
        var catalog = app.Services.GetService<IDeviceTypeCatalog>()
            ?? throw new InvalidOperationException(
                $"The device API can't be mapped because {nameof(IDeviceTypeCatalog)} is not registered. " +
                $"Call {nameof(DevicesBuilderExtensions.AddOosDeviceServices)} first.");

        var root = app.MapGroup("/device/v1");

        foreach (var type in catalog.Types)
        {
            var group = root.MapGroup($"/{type.Key}");
            try
            {
                MapDeviceTypeMethod.MakeGenericMethod(type.DefinitionType).Invoke(null, [group]);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw new InvalidOperationException(
                    $"Mapping the device API for device type \"{type.Key}\" ({type.DefinitionType.FullName}) failed: {ex.InnerException.Message}",
                    ex.InnerException);
            }
        }

        return app;
    }

    /// <summary>
    /// Calls the static <see cref="IDeviceTypeDefinition.MapEndpoints"/> for a type that is only known at
    /// runtime, through <see cref="MethodInfo.MakeGenericMethod"/>.
    /// </summary>
    private static void MapDeviceType<T>(RouteGroupBuilder group) where T : IDeviceTypeDefinition =>
        T.MapEndpoints(group);
}
