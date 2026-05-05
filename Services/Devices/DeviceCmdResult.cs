// OpenOrderSystem.Core/Services/Devices/DeviceCmdResult.cs
using System.Text.Json;

namespace OpenOrderSystem.Core.Services.Devices;

/// <summary>
/// Represents the result of a device command dispatched via <see cref="Interfaces.IDeviceService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsSuccess"/> provides a quick pass/fail check derived from <see cref="Status"/>.
/// <see cref="Status"/> provides granular detail for callers that need to branch on specific
/// failure conditions.
/// </para>
/// <para>
/// <see cref="Data"/> carries any structured payload returned by the handler. Most commands
/// are fire-and-forget and will leave this <see langword="null"/>.
/// </para>
/// </remarks>
public class DeviceCmdResult
{
    /// <summary>
    /// Indicates whether the command completed successfully.
    /// </summary>
    public bool IsSuccess => Status == DeviceCmdStatus.Success;

    /// <summary>
    /// Granular status code describing the outcome of the command.
    /// </summary>
    public DeviceCmdStatus Status { get; init; }

    /// <summary>
    /// Optional structured payload returned by the handler, or <see langword="null"/>
    /// for commands that produce no output.
    /// </summary>
    public Dictionary<string, JsonElement>? Data { get; init; }

    /// <summary>
    /// Optional human-readable message describing the outcome, intended for logging
    /// or diagnostic display.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// The exception that caused this result, if any, or <see langword="null"/> if
    /// the command completed without throwing.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Returns a successful result with an optional data payload.
    /// </summary>
    /// <param name="data">Optional structured payload to return to the caller.</param>
    /// <param name="message">Optional message describing the outcome.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> with <see cref="DeviceCmdStatus.Success"/>.</returns>
    public static DeviceCmdResult Ok(Dictionary<string, JsonElement>? data = null, string? message = null) =>
        new() { Status = DeviceCmdStatus.Success, Data = data, Message = message };

    /// <summary>
    /// Returns a failure result indicating no device was found for the given identifier.
    /// </summary>
    /// <param name="identifier">The key or ID that failed to resolve.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> with <see cref="DeviceCmdStatus.DeviceNotFound"/>.</returns>
    public static DeviceCmdResult NotFound(string identifier) =>
        new() { Status = DeviceCmdStatus.DeviceNotFound, Message = $"No device found matching '{identifier}'." };

    /// <summary>
    /// Returns a failure result indicating no handler is registered for the given device type.
    /// </summary>
    /// <param name="deviceType">The device type that has no registered handler.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> with <see cref="DeviceCmdStatus.HandlerNotFound"/>.</returns>
    public static DeviceCmdResult NoHandler(string deviceType) =>
        new() { Status = DeviceCmdStatus.HandlerNotFound, Message = $"No handler registered for device type '{deviceType}'." };

    /// <summary>
    /// Returns a failure result indicating the handler does not support the given command.
    /// </summary>
    /// <param name="command">The unsupported command name.</param>
    /// <param name="handlerName">The name of the handler that received the command.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> with <see cref="DeviceCmdStatus.CommandNotSupported"/>.</returns>
    public static DeviceCmdResult NotSupported(string command, string handlerName) =>
        new() { Status = DeviceCmdStatus.CommandNotSupported, Message = $"Command '{command}' is not supported by {handlerName}." };

    /// <summary>
    /// Returns a generic failure result.
    /// </summary>
    /// <param name="message">A message describing the reason for failure.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> with <see cref="DeviceCmdStatus.Failure"/>.</returns>
    public static DeviceCmdResult Fail(string message) =>
        new() { Status = DeviceCmdStatus.Failure, Message = message };

    /// <summary>
    /// Returns a failure result representing an unhandled exception during command execution.
    /// </summary>
    /// <param name="command">The command that was being executed.</param>
    /// <param name="deviceType">The device type of the handler that threw.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> with <see cref="DeviceCmdStatus.UnhandledException"/>.</returns>
    public static DeviceCmdResult FromException(string command, string deviceType, Exception ex) =>
        new() { Status = DeviceCmdStatus.UnhandledException, Message = $"Command '{command}' threw an unhandled exception in handler for device type '{deviceType}'.", Exception = ex };
}

/// <summary>
/// Describes the outcome of a device command dispatched via <see cref="Interfaces.IDeviceService"/>.
/// </summary>
public enum DeviceCmdStatus
{
    /// <summary>
    /// The command completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The command failed for an unspecified reason. See <see cref="DeviceCmdResult.Message"/>
    /// and <see cref="DeviceCmdResult.Exception"/> for detail.
    /// </summary>
    Failure,

    /// <summary>
    /// No device was found in the registry matching the provided key or identifier.
    /// </summary>
    DeviceNotFound,

    /// <summary>
    /// A device was found in the registry but no handler is registered for its
    /// <see cref="Data.DataModels.V2.Devices.DeviceHead.DeviceType"/>.
    /// </summary>
    HandlerNotFound,

    /// <summary>
    /// The handler for this device does not support the requested command.
    /// </summary>
    CommandNotSupported,

    /// <summary>
    /// The handler was found and invoked but threw an unhandled exception during execution.
    /// See <see cref="DeviceCmdResult.Exception"/> for detail.
    /// </summary>
    UnhandledException
}