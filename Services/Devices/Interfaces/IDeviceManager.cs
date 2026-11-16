using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.Devices;

namespace OpenOrderSystem.Core.Services.Devices.Interfaces;

public interface IDeviceManager
{
    Task<DeviceResult> EnrollDeviceAsync(string key, string name, DeviceFlags flags,
        IReadOnlyDictionary<string, JsonElement> configs);
}

public class DeviceResult
{
    public bool Success { get; set; }
}