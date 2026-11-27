using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;
using OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;
using Quartz;

namespace OpenOrderSystem.Core.Services.Ordering.Jobs;

/// <summary>
/// The order tick: promotes queued orders that are due into their entry stage, then advances active
/// orders whose stage time has passed.
/// </summary>
/// <remarks>
/// <para>
/// Always enabled. It is not listed in the schedule manifest, and its run interval comes from the general
/// config store (<c>oos:order_advance_interval_seconds</c>), defaulting to
/// <see cref="DEFAULT_RUN_INTERVAL_SECONDS"/> seconds.
/// </para>
/// <para>
/// The work itself is done by <see cref="IOrderStageService"/>. This job only calls it on a timer, and
/// staff can still promote or advance an order straight away through that service.
/// </para>
/// </remarks>
public class AutomaticOrderAdvanceJob : IOosJob
{
    /// <summary>
    /// The run interval used when the config store has no value for it.
    /// </summary>
    private const int DEFAULT_RUN_INTERVAL_SECONDS = 20;

    /// <summary>
    /// The config store key holding the run interval, in seconds.
    /// </summary>
    private const string INTERVAL_SECONDS_CONFIG_KEY = "oos:order_advance_interval_seconds";

    /// <summary>
    /// Schedules the job to run every configured interval, starting now and repeating until the schedule is
    /// rebuilt.
    /// </summary>
    /// <remarks>
    /// The interval is read once, here, so a change to it takes effect on the next schedule rebuild. When a
    /// run is missed, for example because the previous one ran long, the job runs straight away once and
    /// then returns to its interval.
    /// </remarks>
    /// <inheritdoc cref="IOosJob.RegisterAsync"/>
    public static async Task RegisterAsync(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken)
    {
        var configStore = services.GetRequiredService<IConfigurationStore>();
        var interval = await configStore.GetConfigurationAsync<int>(INTERVAL_SECONDS_CONFIG_KEY, DEFAULT_RUN_INTERVAL_SECONDS);

        var job = JobBuilder.Create<AutomaticOrderAdvanceJob>()
            .WithIdentity(nameof(AutomaticOrderAdvanceJob), IOosJob.Group)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{nameof(AutomaticOrderAdvanceJob)}-trigger")
            .StartNow()
            .WithSimpleSchedule(s => s
                .WithIntervalInSeconds(interval)
                .RepeatForever()
                .WithMisfireHandlingInstructionNowWithRemainingCount())
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }

    private readonly IOrderStageService _orderStageService;

    /// <summary>
    /// Initializes a new instance of <see cref="AutomaticOrderAdvanceJob"/>. Quartz creates one for each run.
    /// </summary>
    /// <param name="orderStageService">The stage service for this run's scope.</param>
    public AutomaticOrderAdvanceJob(IOrderStageService orderStageService)
    {
        _orderStageService = orderStageService;
    }

    /// <summary>
    /// Promotes every queued order that is due, then advances every active order whose stage time has passed.
    /// </summary>
    /// <remarks>
    /// Promotion runs first. An order promoted in this run gets a stage advance time measured from now, so it
    /// is only advanced in the same run when its entry stage has an auto-complete time of zero.
    /// </remarks>
    /// <param name="context">The Quartz execution context.</param>
    public async Task Execute(IJobExecutionContext context)
    {
        await _orderStageService.PromoteDueOrdersAsync();
        await _orderStageService.AdvanceDueOrdersAsync();
    }
}
