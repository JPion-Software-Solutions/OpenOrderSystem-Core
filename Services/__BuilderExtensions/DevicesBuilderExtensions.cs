using OpenOrderSystem.Core.Data.DataModels.V2.Devices;
using OpenOrderSystem.Core.Services.Devices.Types;
using OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

/// <summary>
/// The device service group: device types and everything that manages devices and their work.
/// </summary>
public static class DevicesBuilderExtensions
{
    /// <summary>
    /// Registers the device service group. Safe to call more than once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Today this registers the <see cref="IDeviceTypeCatalog"/>, found by scanning every loaded
    /// assembly (see <see cref="AddDeviceTypes"/>). The rest of the group joins it here as it is built.
    /// </para>
    /// <para>
    /// Only assemblies already loaded into the app domain are scanned, so load plugin assemblies before
    /// calling this.
    /// </para>
    /// </remarks>
    /// <param name="bob">The web application builder.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if device type registration fails. See <see cref="AddDeviceTypes"/>.
    /// </exception>
    public static WebApplicationBuilder AddOosDeviceServices(this WebApplicationBuilder bob)
    {
        ArgumentNullException.ThrowIfNull(bob);

        AddDeviceTypes(bob.Services);

        return bob;
    }

    /// <summary>
    /// Scans every loaded assembly that can contain device types and registers the singleton
    /// <see cref="IDeviceTypeCatalog"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every non-abstract class with <see cref="DeviceTypeAttribute"/> is added to the catalog. The
    /// definition classes are not registered in DI; they only have static members, and
    /// <see cref="API.Device.__Extensions.MapDeviceApiRoutes"/> reads them from the catalog. Keys must
    /// be unique across all assemblies and fit in <see cref="DeviceHead.DeviceType"/>.
    /// </para>
    /// <para>
    /// Only this assembly and assemblies that reference it are scanned (see
    /// <see cref="AttributedTypeScanner.FindAttributed{TAttribute}"/>). Does nothing if the
    /// catalog is already registered.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown, listing every problem and the types and assemblies involved, if an assembly's types
    /// cannot be loaded, two device types share a key, a key is blank or longer than
    /// <see cref="DeviceHead.DeviceTypeMaxLength"/> characters, or a class with the attribute does not
    /// implement <see cref="IDeviceTypeDefinition"/> or is an open generic type. Nothing is registered
    /// when this is thrown.
    /// </exception>
    private static void AddDeviceTypes(IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(IDeviceTypeCatalog))) return;

        var errors = new List<string>();
        var found = AttributedTypeScanner.FindAttributed<DeviceTypeAttribute>(
            typeof(IDeviceTypeDefinition), a => a.Key, "Device type", errors);

        foreach (var (type, attribute) in found)
        {
            if (attribute.Key is { Length: > DeviceHead.DeviceTypeMaxLength })
                errors.Add($"{AttributedTypeScanner.Describe(type)} has [DeviceType] with a key of {attribute.Key.Length} characters, but keys may be at most {DeviceHead.DeviceTypeMaxLength} to fit {nameof(DeviceHead)}.{nameof(DeviceHead.DeviceType)}.");
        }

        AttributedTypeScanner.ThrowIfAny(errors, "Device type registration failed:");

        var descriptors = found.Select(f => new DeviceTypeDescriptor(
            f.Attribute.Key,
            f.Attribute.Role,
            string.IsNullOrWhiteSpace(f.Attribute.DisplayName) ? f.Attribute.Key : f.Attribute.DisplayName,
            f.Attribute.Description,
            f.Type));

        services.AddSingleton<IDeviceTypeCatalog>(new DeviceTypeCatalog(descriptors));
    }
}
