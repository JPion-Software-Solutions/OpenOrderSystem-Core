using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1.PrintBridge;

/// <summary>
/// The print bridge device type: a local service that talks to OOS and drives the printers under it.
/// </summary>
/// <remarks>
/// A stub for now. The print bridge's device API endpoints will be mapped here.
/// </remarks>
[DeviceType("print-bridge", DeviceRole.Principal,
    DisplayName = "Print Bridge",
    Description = "A local service that connects to OOS and drives the printers attached to it.")]
public sealed class PrintBridgeDeviceType : IDeviceTypeDefinition
{
    private PrintBridgeDeviceType() { }

    /// <inheritdoc />
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        // No endpoints yet.
    }
}
