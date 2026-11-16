using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1.Printer;

/// <summary>
/// The printer device type: a receipt or ticket printer managed through a print bridge.
/// </summary>
/// <remarks>
/// A stub for now. Printers are dependent devices and never call the device API themselves; their
/// print bridge claims their jobs.
/// </remarks>
[DeviceType("printer", DeviceRole.Dependent,
    DisplayName = "Printer",
    Description = "A receipt or ticket printer driven by a print bridge.")]
public sealed class PrinterDeviceType : IDeviceTypeDefinition
{
    private PrinterDeviceType() { }

    /// <inheritdoc />
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        // Printers are dependent devices and never call the device API themselves, so nothing is mapped.
    }
}
