// OpenOrderSystem.Core/Services/Devices/DeviceHandlerBase.cs
using System.Reflection;
using System.Text.Json;
using OpenOrderSystem.Core.Services.Devices.Attributes;
using OpenOrderSystem.Core.Services.Devices.Interfaces;

namespace OpenOrderSystem.Core.Services.Devices;

/// <summary>
/// Abstract base class for OOS device handlers providing automatic attribute-based
/// command dispatch via <see cref="DeviceExecTargetAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Subclasses may decorate methods with <see cref="DeviceExecTargetAttribute"/> to map
/// them to specific command strings. When <see cref="ExecAsync"/> is called, the base
/// implementation reflects over the subclass to find a matching target method and invokes it.
/// </para>
/// <para>
/// Overriding <see cref="ExecAsync"/> gives full control over dispatch. Calling
/// <c>base.ExecAsync(command, data)</c> from an override falls through to attribute-based
/// dispatch for any commands not handled explicitly.
/// </para>
/// <para>
/// Target methods must match the signature:
/// <c>Task&lt;DeviceCmdResult&gt; MethodName(string command, Dictionary&lt;string, JsonElement&gt; data)</c>
/// </para>
/// </remarks>
public abstract class DeviceHandlerBase : IDeviceHandler
{
    private readonly Dictionary<string, MethodInfo> _dispatchMap;

    /// <summary>
    /// The device type identifier declared on this handler via <see cref="DeviceHandlerInfoAttribute"/>.
    /// </summary>
    protected string DeviceType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceHandlerBase"/> and builds the
    /// command dispatch map from <see cref="DeviceExecTargetAttribute"/> decorations
    /// on the subclass.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the subclass is not decorated with <see cref="DeviceHandlerInfoAttribute"/>.
    /// </exception>
    protected DeviceHandlerBase()
    {
        var info = GetType().GetCustomAttribute<DeviceHandlerInfoAttribute>()
            ?? throw new InvalidOperationException(
                $"{GetType().Name} must be decorated with {nameof(DeviceHandlerInfoAttribute)}.");

        DeviceType = info.DeviceType;

        _dispatchMap = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

        var methods = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var method in methods)
        {
            var targets = method.GetCustomAttributes<DeviceExecTargetAttribute>();
            foreach (var target in targets)
                _dispatchMap[target.Command] = method;
        }
    }

    /// <summary>
    /// Executes a command against this handler using attribute-based dispatch.
    /// </summary>
    /// <param name="recipient">The recipient on which the command will be executed</param>
    /// <param name="command">The command name to execute.</param>
    /// <param name="data">A dictionary of named parameters for the command.</param>
    /// <returns>A <see cref="DeviceCmdResult"/> describing the outcome of the command.</returns>
    /// <remarks>
    /// Override this method for full control over dispatch. Call <c>base.ExecAsync(command, data)</c>
    /// to fall through to attribute-based dispatch for unhandled commands.
    /// </remarks>
    public virtual async Task<DeviceCmdResult> ExecAsync(DeviceContext recipient, string command, Dictionary<string, JsonElement> data)
    {
        if (!_dispatchMap.TryGetValue(command, out var method))
            return DeviceCmdResult.NotSupported(command, DeviceType);

        try
        {
            var result = method.Invoke(this, [recipient, command, data]);

            return result is Task<DeviceCmdResult> task
                ? await task
                : DeviceCmdResult.Fail(
                    $"Command target '{method.Name}' on recipient type '{DeviceType}' did not return Task<DeviceCmdResult>.");
        }
        catch (Exception ex)
        {
            return DeviceCmdResult.FromException(command, DeviceType, ex);
        }
    }
}