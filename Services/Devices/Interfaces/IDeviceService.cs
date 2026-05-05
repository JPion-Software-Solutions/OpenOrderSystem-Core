// OpenOrderSystem.Core/Services/Devices/Interfaces/IDeviceService.cs
using System.Text.Json;

namespace OpenOrderSystem.Core.Services.Devices.Interfaces;

/// <summary>
/// Top-level service for authenticating devices and dispatching commands to registered
/// OOS devices.
/// </summary>
/// <remarks>
/// <para>
/// All device interaction in OOS should go through this service. Handlers are never
/// exposed directly to callers — resolution, authentication, permission enforcement,
/// and dispatch are handled internally.
/// </para>
/// <para>
/// A device must authenticate via <see cref="AuthenticateDevice(string, string)"/> or
/// <see cref="AuthenticateDevice(Guid, string)"/> before invoking commands that require
/// authentication. Unauthenticated callers may only invoke commands on public handler
/// methods — protected methods require a valid <see cref="SenderContext"/>.
/// </para>
/// <para>
/// Since <see cref="IDeviceService"/> is registered as a scoped service, each request
/// gets its own instance with its own authentication state. Authentication state does
/// not persist across requests.
/// </para>
/// <para>
/// Devices may be addressed by their human-friendly <see cref="Data.DataModels.V2.Devices.DeviceHead.Key"/>
/// or by their <see cref="Data.DataModels.V2.Devices.DeviceHead.Id"/>.
/// </para>
/// </remarks>
public interface IDeviceService
{
    /// <summary>
    /// The <see cref="DeviceContext"/> of the authenticated sender, or
    /// <see langword="null"/> if no device has authenticated on this instance.
    /// </summary>
    DeviceContext? SenderContext { get; }

    /// <summary>
    /// The resolved permission set of the authenticated sender, or
    /// <see langword="null"/> if no device has authenticated on this instance.
    /// Combines type-level default permissions from <see cref="Attributes.DevicePermissionAttribute"/>
    /// with any instance-level overrides from <see cref="Data.DataModels.V2.Devices.DeviceConfig"/>.
    /// </summary>
    HashSet<string>? Permissions { get; }

    /// <summary>
    /// Indicates whether a device has successfully authenticated on this instance.
    /// </summary>
    bool IsAuthenticated() => SenderContext != null;

    /// <summary>
    /// Authenticates a device by key and secret, populating <see cref="SenderContext"/>
    /// and <see cref="Permissions"/> on success.
    /// </summary>
    /// <param name="deviceKey">The unique key of the device to authenticate.</param>
    /// <param name="deviceSecret">The raw device secret to validate against the stored hash.</param>
    /// <returns>
    /// This <see cref="IDeviceService"/> instance for fluent chaining. If authentication
    /// fails, <see cref="SenderContext"/> and <see cref="Permissions"/> remain
    /// <see langword="null"/>.
    /// </returns>
    Task<IDeviceService> AuthenticateDevice(string deviceKey, string deviceSecret);

    /// <summary>
    /// Authenticates a device by unique identifier and secret, populating
    /// <see cref="SenderContext"/> and <see cref="Permissions"/> on success.
    /// </summary>
    /// <param name="deviceId">The unique identifier of the device to authenticate.</param>
    /// <param name="deviceSecret">The raw device secret to validate against the stored hash.</param>
    /// <returns>
    /// This <see cref="IDeviceService"/> instance for fluent chaining. If authentication
    /// fails, <see cref="SenderContext"/> and <see cref="Permissions"/> remain
    /// <see langword="null"/>.
    /// </returns>
    Task<IDeviceService> AuthenticateDevice(Guid deviceId, string deviceSecret);

    /// <summary>
    /// Dispatches a command to the device identified by the given key.
    /// </summary>
    /// <param name="deviceKey">The unique key of the target device (e.g., <c>"printer-kitchen-1"</c>).</param>
    /// <param name="command">The command name to execute.</param>
    /// <param name="data">A dictionary of named parameters for the command.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> describing the outcome of the command.</returns>
    Task<DeviceCmdResult> ExecAsync(string deviceKey, string command, Dictionary<string, JsonElement> data);

    /// <summary>
    /// Dispatches a command to the device identified by the given unique identifier.
    /// </summary>
    /// <param name="deviceId">The unique identifier of the target device.</param>
    /// <param name="command">The command name to execute.</param>
    /// <param name="data">A dictionary of named parameters for the command.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> describing the outcome of the command.</returns>
    Task<DeviceCmdResult> ExecAsync(Guid deviceId, string command, Dictionary<string, JsonElement> data);
}