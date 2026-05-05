namespace OpenOrderSystem.Core.Services.Devices.Attributes;

/// <summary>
/// Decorates an <see cref="Interfaces.IDeviceHandler"/> implementation to declare a
/// supporting service dependency with an explicit lifetime that differs from the handler's own.
/// </summary>
/// <remarks>
/// <para>
/// Apply one or more instances of this attribute to a handler class alongside
/// <see cref="DeviceHandlerInfoAttribute"/> for each dependency that requires a lifetime
/// other than the handler's own. The scanner will register each declared dependency
/// using its specified lifetime.
/// </para>
/// <para>
/// For dependencies that should share the handler's lifetime, use the
/// <see cref="DeviceHandlerInfoAttribute(string, ServiceLifetime, Type[])"/> overload instead.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class HandlerDependencyAttribute : Attribute
{
    /// <summary>
    /// The dependency type to register.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// The service lifetime to register the dependency with.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="HandlerDependencyAttribute"/>.
    /// </summary>
    /// <param name="type">The dependency type to register.</param>
    /// <param name="lifetime">The service lifetime to register the dependency with.</param>
    public HandlerDependencyAttribute(Type type, ServiceLifetime lifetime)
    {
        Type = type;
        Lifetime = lifetime;
    }
}