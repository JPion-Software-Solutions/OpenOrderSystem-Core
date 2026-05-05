// OpenOrderSystem.Core/Services/Devices/Attributes/DevicePermissionAttribute.cs
using Microsoft.Extensions.DependencyInjection;

namespace OpenOrderSystem.Core.Services.Devices.Attributes;

/// <summary>
/// Declares a permission granted by default to any device whose handler is decorated
/// with this attribute. Multiple instances may be applied to declare multiple permissions.
/// </summary>
/// <remarks>
/// <para>
/// Permissions declared here represent the type-level default permission set for a device.
/// Instance-level overrides may be applied via <see cref="Data.DataModels.V2.Devices.DeviceConfig"/>
/// entries at enrollment time.
/// </para>
/// <para>
/// By convention, permission strings should be namespaced with a colon-separated prefix
/// indicating scope (e.g., <c>device:can-enroll-devices</c>, <c>user:can-manage-orders</c>).
/// </para>
/// <para>
/// All declared permissions are catalogued by the device handler scanner at boot time
/// into a system-wide permission registry, making them available for admin UI display
/// and runtime permission validation.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class DevicePermissionAttribute : Attribute
{
    /// <summary>
    /// The permission string granted by this declaration.
    /// By convention, namespaced with a colon-separated prefix (e.g., <c>device:can-enroll-devices</c>).
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// A human-readable description of this permission, used in the admin UI
    /// and permission registry.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Initializes a new instance of <see cref="DevicePermissionAttribute"/>.
    /// </summary>
    /// <param name="permission">The permission string to declare.</param>
    public DevicePermissionAttribute(string permission)
    {
        Permission = permission;
    }
}