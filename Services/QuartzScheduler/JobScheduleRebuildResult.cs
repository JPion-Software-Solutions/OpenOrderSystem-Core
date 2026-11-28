namespace OpenOrderSystem.Core.Services.QuartzScheduler;

/// <summary>
/// The outcome of <see cref="Interfaces.IJobScheduleService.RebuildAsync"/>.
/// </summary>
/// <param name="JobTypeCount">How many <see cref="Interfaces.IOosJob"/> types were found.</param>
/// <param name="ScheduledJobCount">How many jobs are in <see cref="Interfaces.IOosJob.Group"/> after the rebuild.</param>
/// <param name="Failures">The job types whose registration threw, in the order they were tried.</param>
public sealed record JobScheduleRebuildResult(
    int JobTypeCount,
    int ScheduledJobCount,
    IReadOnlyList<JobRegistrationFailure> Failures)
{
    /// <summary>True when every job type registered.</summary>
    public bool Succeeded => Failures.Count == 0;

    /// <summary>A one-line summary for logs and post-boot task results.</summary>
    public override string ToString()
    {
        var summary = $"{ScheduledJobCount} job(s) scheduled from {JobTypeCount} job type(s).";
        return Succeeded
            ? summary
            : $"{summary} {Failures.Count} failed to register: {string.Join("; ", Failures.Select(f => $"{f.JobType.Name}: {f.Message}"))}";
    }
}

/// <summary>
/// A job type whose <see cref="Interfaces.IOosJob.RegisterAsync"/> threw during a rebuild.
/// </summary>
/// <param name="JobType">The job type.</param>
/// <param name="Message">The exception message. The full exception is logged by the rebuild.</param>
public sealed record JobRegistrationFailure(Type JobType, string Message);
