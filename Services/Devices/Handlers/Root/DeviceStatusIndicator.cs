// OpenOrderSystem.Core/Services/Devices/Handlers/Root/DeviceStatusIndicator.cs
namespace OpenOrderSystem.Core.Services.Devices.Handlers.Root;

/// <summary>
/// Indicates the operational status of a registered OOS device as last reported
/// via the <c>ReportStatus</c> command on <see cref="OosRootHandler"/>.
/// </summary>
public enum DeviceStatusIndicator
{
    /// <summary>
    /// The device is online and operating normally.
    /// </summary>
    Online,

    /// <summary>
    /// The device is online but operating in a degraded state.
    /// See the accompanying telemetry payload for detail.
    /// </summary>
    Degraded,

    /// <summary>
    /// The device is offline or unreachable.
    /// </summary>
    Offline
}