using OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;
using OpenOrderSystem.Core.Services.QuartzScheduler.Manifest;
using Quartz;

namespace OpenOrderSystem.Core.Services.QuartzScheduler.Jobs;

/// <summary>
/// A minimal optional job that writes a log line on its schedule. It exists to exercise the schedule
/// manifest end to end, and as the smallest example of a manifest-driven job.
/// </summary>
/// <remarks>
/// Not scheduled unless the manifest lists it. Example entry:
/// <code>
/// "system.heartbeat": { "instances": { "every-minute": { "cron": "0 * * * * ?", "settings": { "message": "still here" } } } }
/// </code>
/// </remarks>
[OosQuartzJob("system.heartbeat",
    Name = "Heartbeat",
    Description = "Writes a log line on a schedule, to confirm scheduled jobs are running.",
    Source = "OOS Core")]
[DisallowConcurrentExecution]
public class HeartbeatJob : IOosJob
{
    /// <summary>The settings each heartbeat instance takes.</summary>
    public sealed class Settings
    {
        /// <summary>The text written with each heartbeat.</summary>
        public string Message { get; set; } = "Heartbeat";
    }

    /// <summary>Schedules one heartbeat per enabled instance in the manifest.</summary>
    /// <inheritdoc cref="IOosJob.RegisterAsync"/>
    public static Task RegisterAsync(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken) =>
        services.GetRequiredService<IJobScheduleService>()
            .ScheduleFromManifestAsync<HeartbeatJob>(services, scheduler, cancellationToken);

    private readonly ILogger<HeartbeatJob> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="HeartbeatJob"/>. Quartz creates one for each run.
    /// </summary>
    public HeartbeatJob(ILogger<HeartbeatJob> logger)
    {
        _logger = logger;
    }

    /// <summary>Writes the instance's message to the log.</summary>
    public Task Execute(IJobExecutionContext context)
    {
        var settings = context.GetManifestSettings<Settings>();
        _logger.LogInformation("Heartbeat '{Instance}': {Message}", context.GetManifestInstanceId(), settings.Message);
        return Task.CompletedTask;
    }
}
