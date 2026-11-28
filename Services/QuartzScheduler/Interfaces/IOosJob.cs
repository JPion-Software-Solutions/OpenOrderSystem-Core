using Quartz;

namespace OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;

/// <summary>
/// A Quartz job that OOS discovers and schedules itself.
/// </summary>
/// <remarks>
/// <para>
/// Every non-abstract class implementing this interface is found by
/// <see cref="IJobScheduleService.RebuildAsync"/>, which calls its <see cref="RegisterAsync"/>
/// to add it to the scheduler. A job does not need registering anywhere else.
/// </para>
/// <para>
/// The schedule is rebuilt by deleting every job in <see cref="Group"/> and registering them all again,
/// so jobs and triggers added in <see cref="RegisterAsync"/> must be in that group. Anything outside it is
/// not removed on a rebuild and will be added twice.
/// </para>
/// </remarks>
public interface IOosJob : IJob
{
    /// <summary>
    /// The Quartz group every <see cref="IOosJob"/> job and trigger is kept in.
    /// </summary>
    const string Group = "oos";

    /// <summary>
    /// Adds this job to the scheduler, if and as many times as it should run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called on each schedule rebuild, after every job in <see cref="Group"/> has been deleted, so it never
    /// finds its own earlier registrations. A job that is always enabled schedules itself from the general
    /// config store and its hard-coded defaults. An optional job checks the schedule manifest for whether
    /// it runs and how many instances it has.
    /// </para>
    /// <para>
    /// Throw only for failures worth retrying, such as the database not being ready yet. The other jobs are
    /// still registered, but the rebuild reports failure and is retried, and a rebuild that is required and
    /// keeps failing stops the app. Log and skip problems that retrying cannot fix, such as a bad manifest
    /// entry.
    /// </para>
    /// </remarks>
    /// <param name="services">The service provider of a scope created for this rebuild.</param>
    /// <param name="scheduler">The scheduler to add the job to.</param>
    /// <param name="cancellationToken">Cancels the registration.</param>
    static abstract Task RegisterAsync(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken);
}
