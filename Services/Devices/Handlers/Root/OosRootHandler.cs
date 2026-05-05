// OpenOrderSystem.Core/Services/Devices/Handlers/Root/OosRootHandler.cs
using System.Text.Json;
using OpenOrderSystem.Core.Services.Devices.Attributes;

namespace OpenOrderSystem.Core.Services.Devices.Handlers.Root;

/// <summary>
/// Device handler for the <c>oos-root</c> device, the central messaging and telemetry
/// anchor of the OOS device system.
/// </summary>
/// <remarks>
/// <para>
/// <c>oos-root</c> is automatically enrolled on first boot and is the ancestor of all
/// other devices in the device tree. This handler is responsible for receiving health
/// telemetry from other devices and servicing device status requests.
/// </para>
/// <para>
/// Supported commands:
/// <list type="bullet">
/// <item><description><c>Ping</c> — confirms <c>oos-root</c> is alive and responsive.</description></item>
/// <item><description><c>ReportStatus</c> — accepts a health telemetry report from a device.</description></item>
/// <item><description><c>GetStatus</c> — returns the last known status of a specific device.</description></item>
/// </list>
/// </para>
/// </remarks>
[DeviceHandlerInfo("oos-root")]
[HandlerDependency(typeof(DeviceTelemetryStore), ServiceLifetime.Singleton)]
public class OosRootHandler : DeviceHandlerBase
{
    private readonly DeviceTelemetryStore _telemetry;
    private readonly ILogger<OosRootHandler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OosRootHandler"/>.
    /// </summary>
    /// <param name="telemetry">The telemetry store for device status reports.</param>
    /// <param name="logger">Logger for this handler.</param>
    public OosRootHandler(DeviceTelemetryStore telemetry, ILogger<OosRootHandler> logger)
    {
        _telemetry = telemetry;
        _logger = logger;
    }

    /// <summary>
    /// Confirms that <c>oos-root</c> is alive and responsive.
    /// </summary>
    /// <param name="device">The device on which the command will be executed</param>
    /// <param name="command">The command name.</param>
    /// <param name="data">Unused for this command.</param>
    /// <returns>A successful <see cref="DeviceCmdResult"/>.</returns>
    [DeviceExecTarget("Ping")]
    public Task<DeviceCmdResult> Ping(DeviceContext device, string command, Dictionary<string, JsonElement> data)
    {
        _logger.LogDebug("oos-root received Ping.");
        return Task.FromResult(DeviceCmdResult.Ok(message: "oos-root is online."));
    }

    /// <summary>
    /// Accepts a health telemetry report from a device and records it in the
    /// <see cref="DeviceTelemetryStore"/>.
    /// </summary>
    /// <param name="device">The device on which the command will be executed</param>
    /// <param name="command">The command name.</param>
    /// <param name="data">
    /// Expected keys:
    /// <list type="bullet">
    /// <item><description><c>deviceKey</c> — the reporting device's unique key.</description></item>
    /// <item><description><c>status</c> — the <see cref="DeviceStatusIndicator"/> value as a string.</description></item>
    /// <item><description>Any additional keys are stored as supplemental telemetry data.</description></item>
    /// </list>
    /// </param>
    /// <returns>A <see cref="DeviceCmdResult"/> indicating whether the report was accepted.</returns>
    [DeviceExecTarget("ReportStatus")]
    public Task<DeviceCmdResult> ReportStatus(DeviceContext device, string command, Dictionary<string, JsonElement> data)
    {
        if (!data.TryGetValue("deviceKey", out var deviceKeyElement))
            return Task.FromResult(DeviceCmdResult.Fail("ReportStatus requires a 'deviceKey' value."));

        if (!data.TryGetValue("status", out var statusElement))
            return Task.FromResult(DeviceCmdResult.Fail("ReportStatus requires a 'status' value."));

        var deviceKey = deviceKeyElement.GetString();
        if (string.IsNullOrWhiteSpace(deviceKey))
            return Task.FromResult(DeviceCmdResult.Fail("ReportStatus 'deviceKey' must not be empty."));

        if (!Enum.TryParse<DeviceStatusIndicator>(statusElement.GetString(), ignoreCase: true, out var status))
            return Task.FromResult(DeviceCmdResult.Fail(
                $"ReportStatus 'status' value '{statusElement.GetString()}' is not a valid {nameof(DeviceStatusIndicator)}."));

        var supplemental = data
            .Where(kvp => kvp.Key is not "deviceKey" and not "status")
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var report = new DeviceStatusReport
        {
            DeviceKey = deviceKey,
            Status = status,
            ReportedAt = DateTime.UtcNow,
            Data = supplemental.Count > 0 ? supplemental : null
        };

        _telemetry.Record(report);

        _logger.LogDebug("oos-root recorded status report for device '{DeviceKey}': {Status}.",
            deviceKey, status);

        return Task.FromResult(DeviceCmdResult.Ok(message: $"Status report recorded for device '{deviceKey}'."));
    }

    /// <summary>
    /// Returns the last known status report for a specific device.
    /// </summary>
    /// <param name="device">The device on which the command will be executed</param>
    /// <param name="command">The command name.</param>
    /// <param name="data">
    /// Expected keys:
    /// <list type="bullet">
    /// <item><description><c>deviceKey</c> — the key of the device to query.</description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// A <see cref="DeviceCmdResult"/> containing the last known status report in its
    /// <see cref="DeviceCmdResult.Data"/> payload, or a failure result if no report exists.
    /// </returns>
    [DeviceExecTarget("GetStatus")]
    public Task<DeviceCmdResult> GetStatus(DeviceContext device, string command, Dictionary<string, JsonElement> data)
    {
        var sender = ExtractSender(data);
        if (sender is null)
        {
            _logger.LogError("{Command} called by unknown sender — 'sender' context missing from payload.", command);
            return Task.FromResult(DeviceCmdResult.Fail($"{command} requires a 'sender' device context in the payload."));
        }
        var deviceKey = sender.Key;

        var report = _telemetry.Get(deviceKey);
        if (report is null)
            return Task.FromResult(DeviceCmdResult.Fail(
                $"No status report found for device '{deviceKey}'."));

        var payload = new Dictionary<string, JsonElement>
        {
            ["deviceKey"] = JsonSerializer.SerializeToElement(report.DeviceKey),
            ["status"] = JsonSerializer.SerializeToElement(report.Status.ToString()),
            ["reportedAt"] = JsonSerializer.SerializeToElement(report.ReportedAt)
        };

        if (report.Data is not null)
            foreach (var kvp in report.Data)
                payload[kvp.Key] = kvp.Value;

        _logger.LogDebug("oos-root served status report for device '{DeviceKey}'.", deviceKey);

        return Task.FromResult(DeviceCmdResult.Ok(payload));
    }
    
    /// <summary>
    /// Resolves the routing targets for a given event based on the requesting device's
    /// configuration.
    /// </summary>
    /// <param name="device">The requesting device whose routing configuration is consulted.</param>
    /// <param name="command">The command name.</param>
    /// <param name="data">
    /// Expected keys:
    /// <list type="bullet">
    /// <item><description><c>event</c> — the event name to resolve targets for (e.g., <c>"order-received"</c>).</description></item>
    /// </list>
    /// </param>
    /// <returns>
    /// A <see cref="DeviceCmdResult"/> containing a serialized list of <see cref="DeviceRoutingTarget"/>
    /// instances in its <see cref="DeviceCmdResult.Data"/> payload, or a failure result if the
    /// event parameter is missing or malformed.
    /// </returns>
    [DeviceExecTarget("Resolve")]
    public Task<DeviceCmdResult> Resolve(DeviceContext device, string command, Dictionary<string, JsonElement> data)
    {
        device = ExtractSender(data) ?? device;
        if (device.DeviceType == "oos-root")
        {
            _logger.LogError("{Command} called by unknown sender — 'sender' context missing from payload.", command);
            return Task.FromResult(DeviceCmdResult.Fail($"{command} requires a 'sender' device context in the payload."));
        }
        
        if (!data.TryGetValue("event", out var eventElement))
        {
            _logger.LogError("Resolve called by '{DeviceKey}' with no 'event' parameter.", device.Key);
            return Task.FromResult(DeviceCmdResult.Fail("Resolve requires an 'event' value."));
        }

        var eventName = eventElement.GetString();
        if (string.IsNullOrWhiteSpace(eventName))
        {
            _logger.LogError("Resolve called by '{DeviceKey}' with an empty 'event' parameter.", device.Key);
            return Task.FromResult(DeviceCmdResult.Fail("Resolve 'event' must not be empty."));
        }

        var prefix = $"routing.{eventName}.";

        var targets = device.Config
            .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => new DeviceRoutingTarget
            {
                DeviceKey = kvp.Key[prefix.Length..],
                Command = kvp.Value
            })
            .ToList();

        if (targets.Count == 0)
        {
            _logger.LogWarning(
                "Resolve found no routing targets for event '{Event}' on device '{DeviceKey}'.",
                eventName, device.Key);
        }

        var payload = new Dictionary<string, JsonElement>
        {
            ["targets"] = JsonSerializer.SerializeToElement(targets)
        };

        return Task.FromResult(DeviceCmdResult.Ok(payload));
    }
    
    private static DeviceContext? ExtractSender(Dictionary<string, JsonElement> data)
    {
        return data.TryGetValue("sender", out var senderElement)
            ? senderElement.Deserialize<DeviceContext>()
            : null;
    }
}