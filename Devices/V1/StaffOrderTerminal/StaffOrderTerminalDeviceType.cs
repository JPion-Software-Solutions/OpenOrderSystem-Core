using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1.StaffOrderTerminal;

/// <summary>
/// The staff order terminal device type: a screen staff use to see and advance orders.
/// </summary>
/// <remarks>
/// A stub for now. The terminal's device API endpoints will be mapped here.
/// </remarks>
[DeviceType("staff-order-terminal", DeviceRole.Principal,
    DisplayName = "Staff Order Terminal",
    Description = "A staff-facing screen that shows incoming orders and advances them through their stages.")]
public sealed class StaffOrderTerminalDeviceType : IDeviceTypeDefinition
{
    private StaffOrderTerminalDeviceType() { }

    /// <inheritdoc />
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        // No endpoints yet.
    }
}
