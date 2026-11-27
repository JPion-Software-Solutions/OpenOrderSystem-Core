namespace OpenOrderSystem.Core.Services.QuartzScheduler;

/// <summary>
/// Gives an OOS Quartz job the context the UI needs to present it to users.
/// </summary>
/// <remarks>
/// <para>
/// Every job that users configure through the UI must carry this attribute. A job configured only
/// through configuration settings, and scheduled by the post-boot scheduling job, does not need it.
/// </para>
/// <para>
/// The attribute is UI/UX context only. It does not schedule the job or affect how the job runs.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
public class OosQuartzJobAttribute : Attribute
{
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
