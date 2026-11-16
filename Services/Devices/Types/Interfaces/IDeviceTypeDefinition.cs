using OpenOrderSystem.Core.API.Device;

namespace OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

/// <summary>
/// Implemented by every device type definition class, alongside <see cref="DeviceTypeAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// The members are <see langword="static abstract"/>, so a definition class is never instantiated. Give it
/// a private constructor. The compiler makes every device type provide each member.
/// </para>
/// <para>
/// Handlers for the mapped endpoints are ordinary static methods, either on the definition class itself
/// or on a separate static API class.
/// </para>
/// </remarks>
public interface IDeviceTypeDefinition
{
    /// <summary>
    /// Maps this device type's device API endpoints.
    /// </summary>
    /// <remarks>
    /// Called once at startup by <see cref="__Extensions.MapDeviceApiRoutes"/>.
    /// </remarks>
    /// <param name="group">
    /// The route group for this device type, already at <c>/device/v1/{key}</c>, where <c>{key}</c> is
    /// the <see cref="DeviceTypeAttribute.Key"/>. Map routes relative to it.
    /// </param>
    static abstract void MapEndpoints(RouteGroupBuilder group);
}
