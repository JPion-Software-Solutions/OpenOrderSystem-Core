// OpenOrderSystem.Core/Services/Devices/Handlers/Root/DeviceStatusReport.cs
using System.Text.Json;

namespace OpenOrderSystem.Core.Services.Devices.Handlers.Root;

/// <summary>
/// Represents a health telemetry report submitted by a device via the
/// <c>ReportStatus</c> command on <see cref="OosRootHandler"/>.
/// </summary>
public class DeviceStatusReport
{
    /// <summary>
    /// The unique key of the reporting device.
    /// </summary>
    public string DeviceKey { get; init; } = string.Empty;

    /// <summary>
    /// The operational status indicator reported by the device.
    /// </summary>
    public DeviceStatusIndicator Status { get; init; }

    /// <summary>
    /// The UTC timestamp at which this report was received by <c>oos-root</c>.
    /// </summary>
    public DateTime ReportedAt { get; init; }

    /// <summary>
    /// Optional device-specific telemetry data accompanying this report.
    /// </summary>
    public Dictionary<string, JsonElement>? Data { get; init; }
}