namespace OpenOrderSystem.Core.Services.Devices.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class DeviceExecTargetAttribute(string command) : Attribute
{
    public string Command { get; } = command;
}