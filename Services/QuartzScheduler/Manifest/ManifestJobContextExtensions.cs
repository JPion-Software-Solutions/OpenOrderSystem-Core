using System.Text.Json;
using Quartz;

namespace OpenOrderSystem.Core.Services.QuartzScheduler.Manifest;

/// <summary>
/// Reads, from inside a run, which manifest instance a job was scheduled as and that instance's settings.
/// </summary>
/// <remarks>
/// Only meaningful in jobs scheduled through
/// <see cref="Interfaces.IJobScheduleService.ScheduleFromManifestAsync{TJob}"/>, which stores both in the
/// job's data map.
/// </remarks>
public static class ManifestJobContextExtensions
{
    /// <summary>The job data map key holding the instance ID.</summary>
    public const string InstanceIdDataKey = "oos.manifest.instance";

    /// <summary>The job data map key holding the instance settings as raw JSON.</summary>
    public const string SettingsDataKey = "oos.manifest.settings";

    /// <summary>The options manifest settings are read with: web defaults, so camelCase and case-insensitive.</summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// The manifest instance ID this run was scheduled as, such as <c>front-counter</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The job was not scheduled from the manifest.</exception>
    public static string GetManifestInstanceId(this IJobExecutionContext context) =>
        context.MergedJobDataMap.GetString(InstanceIdDataKey)
        ?? throw new InvalidOperationException(
            $"Job '{context.JobDetail.Key}' was not scheduled from the schedule manifest, so it has no instance ID.");

    /// <summary>
    /// This instance's settings, read into <typeparamref name="TSettings"/>. An instance with no settings
    /// gives a <typeparamref name="TSettings"/> built from <c>{}</c>, so its defaults apply.
    /// </summary>
    /// <remarks>
    /// The manifest is hand-editable, so settings can be wrong. The exception is left to the job: it can
    /// catch it and run with defaults, or let the run fail.
    /// </remarks>
    /// <exception cref="JsonException">The settings do not fit <typeparamref name="TSettings"/>.</exception>
    public static TSettings GetManifestSettings<TSettings>(this IJobExecutionContext context) where TSettings : new()
    {
        var json = context.MergedJobDataMap.GetString(SettingsDataKey);
        if (string.IsNullOrWhiteSpace(json))
            return new TSettings();

        return JsonSerializer.Deserialize<TSettings>(json, SerializerOptions) ?? new TSettings();
    }
}
