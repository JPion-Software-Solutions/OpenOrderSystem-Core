using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenOrderSystem.Core.Services.QuartzScheduler.Manifest;

/// <summary>
/// The schedule manifest: which optional jobs run, how many instances of each, when, and with what
/// settings. Always-on jobs are not listed here; they schedule themselves from the general config store.
/// </summary>
/// <remarks>
/// <para>
/// Stored as JSON in <c>schedule.json</c> in the data root's config directory, where it can be edited by
/// hand. Changes, from the UI or by hand, take effect on the next schedule rebuild.
/// </para>
/// <para>
/// Immutable, so the copy the current schedule was built from can be shared safely. Build a changed copy
/// with <c>with</c> expressions and save it through
/// <see cref="Interfaces.IJobScheduleService.SaveManifestAsync"/>.
/// </para>
/// </remarks>
public sealed record ScheduleManifest
{
    /// <summary>The manifest format version this build reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>A manifest with no jobs, used when there is no manifest file.</summary>
    public static ScheduleManifest Empty { get; } = new();

    /// <summary>The format version. A file with any other version is not read.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>The optional jobs, keyed by <see cref="OosQuartzJobAttribute.Key"/>.</summary>
    public IReadOnlyDictionary<string, ScheduleManifestJob> Jobs { get; init; } =
        new Dictionary<string, ScheduleManifestJob>(StringComparer.Ordinal);
}

/// <summary>
/// One optional job in the <see cref="ScheduleManifest"/>.
/// </summary>
public sealed record ScheduleManifestJob
{
    /// <summary>
    /// Whether the job runs at all. When false, none of its instances are scheduled, whatever their own
    /// <see cref="ScheduleManifestInstance.Enabled"/> says.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The job's instances, keyed by a stable instance ID such as <c>front-counter</c>. The ID becomes part of
    /// the Quartz job key (<c>&lt;job key&gt;:&lt;instance ID&gt;</c>), so it must not be a list position or
    /// anything else that shifts when another instance is added or removed.
    /// </summary>
    public IReadOnlyDictionary<string, ScheduleManifestInstance> Instances { get; init; } =
        new Dictionary<string, ScheduleManifestInstance>(StringComparer.Ordinal);
}

/// <summary>
/// One scheduled instance of a <see cref="ScheduleManifestJob"/>.
/// </summary>
public sealed record ScheduleManifestInstance
{
    /// <summary>Whether this instance is scheduled.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// A Quartz cron expression (seconds first, such as <c>0 0 21 * * ?</c> for 9 PM daily), read in the
    /// store's configured timezone. An instance with a missing or invalid expression is logged and skipped.
    /// </summary>
    public string? Cron { get; init; }

    /// <summary>
    /// The instance's settings. Loosely typed here: each job reads them into its own settings type with
    /// <see cref="ManifestJobContextExtensions.GetManifestSettings{TSettings}"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Settings { get; init; }
}
