namespace OpenOrderSystem.Core.Services.Devices.Attributes;

/// <summary>
/// Decorates an <see cref="Interfaces.IDeviceHandler"/> implementation to provide
/// device type metadata and service registration hints to the handler scanner.
/// </summary>
/// <remarks>
/// <para>
/// The scanner uses <see cref="DeviceType"/> as the keyed service key and
/// <see cref="Lifetime"/> as the handler's own service lifetime.
/// </para>
/// <para>
/// Dependencies declared via <see cref="DeviceHandlerInfoAttribute(string, ServiceLifetime, Type[])"/>
/// are registered with the handler's own lifetime. For dependencies requiring a distinct
/// lifetime, decorate the handler class with one or more <see cref="HandlerDependencyAttribute"/>
/// instances instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DeviceHandlerInfoAttribute : Attribute
{
    /// <summary>
    /// The device type discriminator used as the keyed service key for this handler.
    /// </summary>
    public string DeviceType { get; }

    /// <summary>
    /// The service lifetime to use when registering this handler.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// Supporting service types to register alongside this handler, each inheriting
    /// the handler's own <see cref="Lifetime"/>.
    /// </summary>
    public HandlerDependency[] Dependencies { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceHandlerInfoAttribute"/> with no
    /// supporting service dependencies.
    /// </summary>
    /// <param name="deviceType">The device type discriminator for this handler.</param>
    /// <param name="lifetime">The service lifetime for this handler. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    public DeviceHandlerInfoAttribute(string deviceType, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        DeviceType = deviceType;
        Lifetime = lifetime;
        Dependencies = [];
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceHandlerInfoAttribute"/> with
    /// supporting service dependencies that inherit the handler's own lifetime.
    /// </summary>
    /// <param name="deviceType">The device type discriminator for this handler.</param>
    /// <param name="lifetime">The service lifetime for this handler and all declared dependencies.</param>
    /// <param name="dependencies">The dependency types to register with the handler's lifetime.</param>
    public DeviceHandlerInfoAttribute(string deviceType, ServiceLifetime lifetime, params Type[] dependencies)
    {
        DeviceType = deviceType;
        Lifetime = lifetime;
        Dependencies = dependencies.Select(t => new HandlerDependency(t, lifetime)).ToArray();
    }

    /// <summary>
    /// Represents a dependency type paired with its intended service lifetime.
    /// Used internally by <see cref="DeviceHandlerInfoAttribute"/> to carry registration
    /// metadata for supporting services declared via the dependency overload.
    /// </summary>
    /// <param name="Type">The dependency type to register.</param>
    /// <param name="Lifetime">The service lifetime to register the dependency with.</param>
    public record HandlerDependency(Type Type, ServiceLifetime Lifetime);
}