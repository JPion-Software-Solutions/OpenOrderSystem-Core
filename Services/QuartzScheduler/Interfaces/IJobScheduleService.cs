using OpenOrderSystem.Core.Services.QuartzScheduler.Manifest;
using Quartz;

namespace OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;

/// <summary>
/// Owns the schedule of every <see cref="IOosJob"/>, and the schedule manifest the optional jobs are
/// scheduled from: building the schedule at boot, and rebuilding it whenever schedule settings change.
/// </summary>
/// <remarks>
/// The schedule is never patched in place. Every change is applied by clearing <see cref="IOosJob.Group"/>
/// and registering every job again. <see cref="SaveManifestAsync"/> rebuilds by itself; whatever else
/// changes a schedule setting, such as an always-on job's interval or the store's timezone, calls
/// <see cref="RebuildAsync"/> afterwards.
/// </remarks>
public interface IJobScheduleService
{
    /// <summary>
    /// The manifest the current schedule was built from: the one read at the start of the last rebuild.
    /// </summary>
    /// <value>
    /// <see cref="ScheduleManifest.Empty"/> before the first rebuild, and when the last rebuild found no
    /// manifest file or could not read it.
    /// </value>
    ScheduleManifest AppliedManifest { get; }

    /// <summary>
    /// Builds the schedule from scratch: reads the manifest, deletes every job in
    /// <see cref="IOosJob.Group"/>, then calls each job's <see cref="IOosJob.RegisterAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rebuilds never overlap: a call waits until any rebuild already running has finished. Starting from an
    /// empty group means a retry, or a rebuild after a change, never leaves stale or duplicate jobs behind.
    /// </para>
    /// <para>
    /// A missing or unreadable manifest never fails the rebuild. It is logged, the rebuild carries on with
    /// <see cref="ScheduleManifest.Empty"/>, and only the always-on jobs are scheduled. Manifest entries that
    /// match no job are logged and ignored.
    /// </para>
    /// <para>
    /// A job that throws from <see cref="IOosJob.RegisterAsync"/> is logged and skipped, and the rest are
    /// still registered. The failure is reported in the result rather than thrown.
    /// </para>
    /// <para>
    /// Deleting a job does not stop a run of it already in progress. That run finishes, and can overlap the
    /// first run of the job registered in its place.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancels the rebuild. It is passed on to each job's registration.</param>
    /// <returns>What was scheduled, and which job types failed to register.</returns>
    Task<JobScheduleRebuildResult> RebuildAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the manifest file as it is now, which may differ from <see cref="AppliedManifest"/> if it was
    /// edited since the last rebuild.
    /// </summary>
    /// <remarks>
    /// Unlike a rebuild, this throws when the file cannot be read, so an editor never mistakes a broken file
    /// for an empty one and overwrites it.
    /// </remarks>
    /// <returns>The manifest, or <see cref="ScheduleManifest.Empty"/> when there is no manifest file.</returns>
    /// <exception cref="System.Text.Json.JsonException">The file is not valid manifest JSON.</exception>
    /// <exception cref="InvalidDataException">The file is empty, or has an unsupported version.</exception>
    Task<ScheduleManifest> ReadManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the manifest file with <paramref name="manifest"/>, then rebuilds the schedule.
    /// </summary>
    /// <remarks>
    /// The manifest is saved as given. Entries that cannot be scheduled, such as an invalid cron expression,
    /// are not rejected here; the rebuild logs and skips them. The file is replaced in one step, so a reader
    /// never sees it half-written.
    /// </remarks>
    /// <param name="manifest">The manifest to save.</param>
    /// <param name="cancellationToken">Cancels the save and the rebuild.</param>
    /// <returns>The result of the rebuild that applied the saved manifest.</returns>
    Task<JobScheduleRebuildResult> SaveManifestAsync(ScheduleManifest manifest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules one Quartz job of <typeparamref name="TJob"/> for each of its enabled instances in
    /// <see cref="AppliedManifest"/>. A manifest-driven job's <see cref="IOosJob.RegisterAsync"/> is a single
    /// call to this.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The job is found in the manifest by its <see cref="OosQuartzJobAttribute.Key"/>. Each instance is
    /// scheduled as job <c>&lt;key&gt;:&lt;instance ID&gt;</c> in <see cref="IOosJob.Group"/>, on a cron trigger
    /// in the store's timezone, with its instance ID and settings in the job data map for
    /// <see cref="ManifestJobContextExtensions"/> to read.
    /// </para>
    /// <para>
    /// Problems a retry cannot fix are logged and skipped, never thrown: a job with no key or a key another
    /// job also declares, an instance with a blank ID, or a missing or invalid cron expression. A store
    /// timezone that cannot be resolved is logged and UTC is used.
    /// </para>
    /// </remarks>
    /// <typeparam name="TJob">The job type being registered.</typeparam>
    /// <param name="services">The rebuild scope's service provider, as passed to <see cref="IOosJob.RegisterAsync"/>.</param>
    /// <param name="scheduler">The scheduler, as passed to <see cref="IOosJob.RegisterAsync"/>.</param>
    /// <param name="cancellationToken">The token passed to <see cref="IOosJob.RegisterAsync"/>.</param>
    /// <returns>How many instances were scheduled.</returns>
    Task<int> ScheduleFromManifestAsync<TJob>(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken)
        where TJob : IOosJob;
}
