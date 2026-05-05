// OpenOrderSystem.Core/Services/Devices/Attributes/DeviceCommandPermissionAttribute.cs
namespace OpenOrderSystem.Core.Services.Devices.Attributes;

/// <summary>
/// Declares the permissions required to invoke a device handler command method.
/// </summary>
/// <remarks>
/// <para>
/// Applied to command methods on <see cref="DeviceHandlerBase"/> subclasses alongside
/// <see cref="DeviceExecTargetAttribute"/>. The device service enforces these requirements
/// before dispatch — the handler never receives the call if the sender lacks the necessary
/// permissions.
/// </para>
/// <para>
/// The <see cref="Required"/> string is a comma-separated list of permission strings
/// evaluated according to <see cref="Type"/>. With <see cref="PermissionType.RequireAll"/>
/// the sender must hold every listed permission. With <see cref="PermissionType.RequireAny"/>
/// the sender must hold at least one.
/// </para>
/// <para>
/// Command methods decorated with this attribute should be <see langword="protected"/>
/// to signal that authentication is required. Public command methods are considered
/// open to any caller.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class DeviceCommandPermissionAttribute : Attribute
{
    /// <summary>
    /// A comma-separated list of permission strings evaluated against the calling
    /// device's permission set according to <see cref="Type"/>.
    /// </summary>
    public string Required { get; }

    /// <summary>
    /// Determines how the permissions in <see cref="Required"/> are evaluated.
    /// Defaults to <see cref="PermissionType.RequireAny"/>.
    /// </summary>
    public PermissionType Type { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceCommandPermissionAttribute"/>.
    /// </summary>
    /// <param name="required">
    /// A comma-separated list of permission strings
    /// (e.g., <c>"device:can-enroll-devices,device:can-manage-children"</c>).
    /// </param>
    /// <param name="type">
    /// How the listed permissions are evaluated. Defaults to <see cref="PermissionType.RequireAny"/>.
    /// </param>
    public DeviceCommandPermissionAttribute(string required, PermissionType type = PermissionType.RequireAny)
    {
        Required = required;
        Type = type;
    }
}

/// <summary>
/// Defines how multiple permissions in a <see cref="DeviceCommandPermissionAttribute"/>
/// are evaluated against a caller's permission set.
/// </summary>
public enum PermissionType
{
    /// <summary>
    /// The caller must hold all listed permissions.
    /// </summary>
    RequireAll,

    /// <summary>
    /// The caller must hold at least one of the listed permissions.
    /// </summary>
    RequireAny
}