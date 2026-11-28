namespace OpenOrderSystem.Core.Services.QuartzScheduler;

/// <summary>
/// Gives an OOS Quartz job a stable key, and the context the UI needs to present it to users.
/// </summary>
/// <remarks>
/// <para>
/// Every job scheduled from the schedule manifest must carry this attribute, because <see cref="Key"/> is
/// what the manifest lists it under. Jobs that users configure through the UI carry it for the same reason.
/// An always-on job, scheduled from the general config store, only needs it to show up in the UI.
/// </para>
/// <para>
/// The attribute does not schedule the job or affect how it runs. Jobs are discovered through the
/// <see cref="Interfaces.IOosJob"/> interface, not this attribute.
/// </para>
/// </remarks>
/// <param name="key">
/// The job's key in the schedule manifest, such as <c>system.heartbeat</c>. It must be unique across every
/// <see cref="Interfaces.IOosJob"/> and must never change once released: renaming it orphans every manifest
/// entry written under the old key. Renaming the class is safe.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OosQuartzJobAttribute(string key) : Attribute
{
    /// <summary>
    /// The job's stable key in the schedule manifest.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Human-readable name of the job, shown to users.
    /// </summary>
    public string? Name { get; init; } = string.Empty;

    /// <summary>
    /// Short description of what the job does, shown to users.
    /// </summary>
    public string? Description { get; init; } = string.Empty;

    /// <summary>
    /// Name of the system that adds the job, so users can see where it comes from.
    /// </summary>
    /// <value>
    /// <see langword="null"/> when no source is given.
    /// </value>
    public string? Source { get; init; } = null;
}
