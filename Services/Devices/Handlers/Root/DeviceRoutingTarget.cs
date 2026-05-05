// OpenOrderSystem.Core/Services/Devices/Handlers/Root/DeviceRoutingTarget.cs
namespace OpenOrderSystem.Core.Services.Devices.Handlers.Root;

/// <summary>
/// Represents a resolved routing target returned by the <c>Resolve</c> command
/// on <see cref="OosRootHandler"/>.
/// </summary>
/// <remarks>
/// Routing targets are derived from the requesting device's <see cref="DeviceContext.Config"/>
/// entries matching the pattern <c>routing.{event}.{deviceKey}</c>, where the value
/// is the command to dispatch to the target device.
/// </remarks>
public class DeviceRoutingTarget
{
    /// <summary>
    /// The unique key of the target device to notify.
    /// </summary>
    public string DeviceKey { get; init; } = string.Empty;

    /// <summary>
    /// The command to dispatch to the target device.
    /// </summary>
    public string Command { get; init; } = string.Empty;
}