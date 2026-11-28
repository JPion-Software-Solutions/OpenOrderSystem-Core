using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;
using OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;
using Quartz;

namespace OpenOrderSystem.Core.Services.Ordering.Jobs;

/// <summary>
/// The ordering tick: promotes queued orders that are due into their entry stage, advances active orders
/// whose stage time has passed, and, at a slower pace, sweeps expired carts.
/// </summary>
/// <remarks>
/// <para>
/// Always enabled. It is not listed in the schedule manifest, and its run interval comes from the general
/// config store (<c>oos:order_advance_interval_seconds</c>), defaulting to
/// <see cref="DEFAULT_RUN_INTERVAL_SECONDS"/> seconds.
/// </para>
/// <para>
/// The work itself is done by <see cref="IOrderStageService"/> and <see cref="ICartCacheService"/>. This job
/// only calls them on a timer, and staff can still promote or advance an order straight away through the
/// stage service.
/// </para>
/// <para>
/// Each step runs in its own try/catch, so a failure in one step is logged and does not skip the others.
/// Runs never overlap.
/// </para>
/// </remarks>
[DisallowConcurrentExecution]
public class OrderingTickJob : IOosJob
{
    /// <summary>
    /// The run interval used when the config store has no usable value for it.
    /// </summary>
    private const int DEFAULT_RUN_INTERVAL_SECONDS = 20;

    /// <summary>
    /// The config store key holding the run interval, in seconds.
    /// </summary>
    private const string INTERVAL_SECONDS_CONFIG_KEY = "oos:order_advance_interval_seconds";

    /// <summary>
    /// The minimum time between cart sweeps. Cart lifetimes are measured in minutes, so sweeping on every
    /// tick would only add database load.
    /// </summary>
    private static readonly TimeSpan CartSweepInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When the last cart sweep was attempted. Static because Quartz creates a new job instance for each
    /// run. It starts at <see cref="DateTimeOffset.MinValue"/>, so the first run after boot sweeps.
    /// </summary>
    private static DateTimeOffset _lastCartSweep = DateTimeOffset.MinValue;

    /// <summary>
    /// Schedules the job to run every configured interval, starting now and repeating until the schedule is
    /// rebuilt.
    /// </summary>
    /// <remarks>
    /// The interval is read once, here, so a change to it takes effect on the next schedule rebuild. A value
    /// of zero or less is logged and replaced with the default, because Quartz rejects it and the failure
    /// would fail the required schedule rebuild. When a run is missed, for example because the previous one
    /// ran long, the job runs straight away once and then returns to its interval.
    /// </remarks>
    /// <inheritdoc cref="IOosJob.RegisterAsync"/>
    public static async Task RegisterAsync(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken)
    {
        var configStore = services.GetRequiredService<IConfigurationStore>();
        var interval = await configStore.GetConfigurationAsync<int>(INTERVAL_SECONDS_CONFIG_KEY, DEFAULT_RUN_INTERVAL_SECONDS);

        if (interval <= 0)
        {
            var logger = services.GetRequiredService<ILogger<OrderingTickJob>>();
            logger.LogWarning(
                "Config key {Key} is {Value}, which is not a valid interval. Using the default of {Default} seconds.",
                INTERVAL_SECONDS_CONFIG_KEY, interval, DEFAULT_RUN_INTERVAL_SECONDS);
            interval = DEFAULT_RUN_INTERVAL_SECONDS;
        }

        var job = JobBuilder.Create<OrderingTickJob>()
            .WithIdentity(nameof(OrderingTickJob), IOosJob.Group)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{nameof(OrderingTickJob)}-trigger", IOosJob.Group)
            .StartNow()
            .WithSimpleSchedule(s => s
                .WithIntervalInSeconds(interval)
                .RepeatForever()
                .WithMisfireHandlingInstructionNowWithRemainingCount())
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }

    private readonly IOrderStageService _orderStageService;
    private readonly ICartCacheService _cartCacheService;
    private readonly ILogger<OrderingTickJob> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OrderingTickJob"/>. Quartz creates one for each run.
    /// </summary>
    /// <param name="orderStageService">The stage service for this run's scope.</param>
    /// <param name="cartCacheService">The singleton cart cache.</param>
    /// <param name="logger">The logger.</param>
    public OrderingTickJob(
        IOrderStageService orderStageService,
        ICartCacheService cartCacheService,
        ILogger<OrderingTickJob> logger)
    {
        _orderStageService = orderStageService;
        _cartCacheService = cartCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Promotes every queued order that is due, advances every active order whose stage time has passed,
    /// then sweeps expired carts if <see cref="CartSweepInterval"/> has passed since the last sweep.
    /// </summary>
    /// <remarks>
    /// Promotion runs first. An order promoted in this run gets a stage advance time measured from now, so it
    /// is only advanced in the same run when its entry stage has an auto-complete time of zero. A failed cart
    /// sweep still counts as an attempt, so a persistent failure is retried at the sweep pace rather than on
    /// every tick.
    /// </remarks>
    /// <param name="context">The Quartz execution context.</param>
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _orderStageService.PromoteDueOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Promoting due orders failed.");
        }

        try
        {
            await _orderStageService.AdvanceDueOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advancing due orders failed.");
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _lastCartSweep < CartSweepInterval)
            return;

        _lastCartSweep = now;
        try
        {
            await _cartCacheService.SweepAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sweeping expired carts failed.");
        }
    }
}
