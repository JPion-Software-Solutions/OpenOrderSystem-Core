using OpenOrderSystem.Core.Services.__BuilderExtensions;

namespace OpenOrderSystem.Core.Services.Devices.Types.Interfaces;

/// <summary>
/// The registered device types, built at startup by
/// <see cref="DevicesBuilderExtensions.AddOosDeviceServices"/>. The source of truth for which device
/// types exist.
/// </summary>
/// <remarks>
/// Used by the admin area when enrolling devices and filtering device pickers, and by
/// <see cref="API.Device.__Extensions.MapDeviceApiRoutes"/> to map each device type's endpoints.
/// </remarks>
public interface IDeviceTypeCatalog
{
    /// <summary>
    /// Every registered device type, ordered by key.
    /// </summary>
    IReadOnlyList<DeviceTypeDescriptor> Types { get; }

    /// <summary>
    /// Looks up a device type by key.
    /// </summary>
    /// <param name="key">The device type key. Matched exactly, case-sensitive.</param>
    /// <param name="descriptor">The device type, when found.</param>
    /// <returns><see langword="true"/> if a device type with that key is registered.</returns>
    bool TryGet(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DeviceTypeDescriptor? descriptor);
}

/// <summary>
/// One registered device type, as declared by its <see cref="DeviceTypeAttribute"/>.
/// </summary>
/// <param name="Key">The device type key.</param>
/// <param name="Role">Whether devices of this type talk to OOS directly or through a parent.</param>
/// <param name="DisplayName">The name shown in the admin area. Falls back to <paramref name="Key"/> when the attribute sets none.</param>
/// <param name="Description">What this kind of device is. <see langword="null"/> when not given.</param>
/// <param name="DefinitionType">
/// The class that declares the device type. Always implements <see cref="IDeviceTypeDefinition"/>.
/// </param>
public sealed record DeviceTypeDescriptor(
    string Key,
    DeviceRole Role,
    string DisplayName,
    string? Description,
    Type DefinitionType);
