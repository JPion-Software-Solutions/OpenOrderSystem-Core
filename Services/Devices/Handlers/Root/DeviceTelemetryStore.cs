// OpenOrderSystem.Core/Services/Devices/Handlers/Root/DeviceTelemetryStore.cs
namespace OpenOrderSystem.Core.Services.Devices.Handlers.Root;

/// <summary>
/// In-memory store for the most recent <see cref="DeviceStatusReport"/> per registered device.
/// </summary>
/// <remarks>
/// <para>
/// Maintained by <see cref="OosRootHandler"/> via the <c>ReportStatus</c> command.
/// Reports are keyed by device key and represent the last known state of each device.
/// </para>
/// <para>
/// This store is registered as a singleton and is not persisted across application restarts.
/// </para>
/// </remarks>
public class DeviceTelemetryStore
{
    private readonly Dictionary<string, DeviceStatusReport> _reports = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <summary>
    /// Records or updates the status report for the given device.
    /// </summary>
    /// <param name="report">The status report to store.</param>
    public void Record(DeviceStatusReport report)
    {
        lock (_lock)
        {
            _reports[report.DeviceKey] = report;
        }
    }

    /// <summary>
    /// Retrieves the last known status report for the given device key,
    /// or <see langword="null"/> if no report has been received.
    /// </summary>
    /// <param name="deviceKey">The device key to look up.</param>
    /// <returns>The most recent <see cref="DeviceStatusReport"/> or <see langword="null"/>.</returns>
    public DeviceStatusReport? Get(string deviceKey)
    {
        lock (_lock)
        {
            return _reports.TryGetValue(deviceKey, out var report) ? report : null;
        }
    }

    /// <summary>
    /// Returns a snapshot of all currently stored device status reports.
    /// </summary>
    /// <returns>A read-only list of the most recent report per device.</returns>
    public IReadOnlyList<DeviceStatusReport> GetAll()
    {
        lock (_lock)
        {
            return _reports.Values.ToList().AsReadOnly();
        }
    }
}