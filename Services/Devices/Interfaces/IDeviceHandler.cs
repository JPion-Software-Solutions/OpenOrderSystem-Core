// OpenOrderSystem.Core/Services/Devices/Interfaces/IDeviceHandler.cs
using System.Text.Json;

namespace OpenOrderSystem.Core.Services.Devices.Interfaces;

/// <summary>
/// Defines the contract for a device handler responsible for processing commands
/// directed at a specific device type in the OOS device registry.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should be decorated with <see cref="Attributes.DeviceHandlerInfoAttribute"/>
/// to participate in handler auto-discovery at startup.
/// </para>
/// <para>
/// For convenience, consider inheriting from <see cref="DeviceHandlerBase"/> instead of
/// implementing this interface directly. <see cref="DeviceHandlerBase"/> provides automatic
/// attribute-based command dispatch via <see cref="Attributes.DeviceExecTargetAttribute"/>.
/// Plugin authors who need full control over dispatch may implement this interface directly.
/// </para>
/// </remarks>
public interface IDeviceHandler
{
    /// <summary>
    /// Executes a command against this handler.
    /// </summary>
    /// <param name="recipient">The recipient on which the command will be executed</param>
    /// <param name="command">The command name to execute.</param>
    /// <param name="data">A dictionary of named parameters for the command.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> describing the outcome of the command.</returns>
    Task<DeviceCmdResult> ExecAsync(DeviceContext recipient, string command, Dictionary<string, JsonElement> data);
}