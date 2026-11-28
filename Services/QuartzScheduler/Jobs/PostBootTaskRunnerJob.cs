using OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;
using Quartz;

namespace OpenOrderSystem.Core.Services.QuartzScheduler.Jobs;

/// <summary>
/// Runs the queued post-boot tasks once the service container is live, retrying failed tasks up to
/// <see cref="MAX_RETRIES"/> times, then removes itself from the scheduler until the next boot.
/// </summary>
/// <remarks>
/// <para>
/// Tasks are queued with <see cref="QueueTask"/> during startup. The queue locks on the first run, so
/// every task must be queued before the scheduler starts.
/// </para>
/// <para>
/// Tasks run in descending <see cref="PostBootTask.Priority"/>, and tasks with the same priority run in
/// the order they were queued. A task that succeeds leaves the queue. A task that fails, by returning an
/// unsuccessful result or by throwing, stays queued and runs again on the next fire. A failure does not
/// stop the tasks after it from running. Once the queue is empty, the job deletes itself and its trigger.
/// </para>
/// <para>
/// A task that fails on its last allowed attempt is given up on. An optional task is dropped with an
/// error in the log and the app keeps running. A <see cref="PostBootTask.Required"/> task logs a critical
/// error, sets a non-zero exit code and stops the app, because OOS cannot run correctly without it.
/// </para>
/// <para>
/// Each task runs in its own DI scope, so one task's failure cannot leave scoped services, such as a
/// half-used <c>DbContext</c>, behind for the next task.
/// </para>
/// <para>
/// This is the only OOS job that is a plain <see cref="IJob"/>. It lives in <see cref="Group"/>, apart from
/// the <c>IOosJob</c> jobs, so clearing and rebuilding their schedule never removes it.
/// </para>
/// </remarks>
[DisallowConcurrentExecution]
public class PostBootTaskRunnerJob : IJob
{
    /// <summary>
    /// The Quartz group that system jobs, such as this runner, are kept in.
    /// </summary>
    public const string Group = "system";

    /// <summary>
    /// How many times a failed task is retried after its first attempt before it is given up on.
    /// </summary>
    public const int MAX_RETRIES = 5;

    /// <summary>
    /// The exit code the app stops with when a required task is given up on.
    /// </summary>
    public const int REQUIRED_TASK_FAILED_EXIT_CODE = 1;

    /// <summary>
    /// The key this job is registered under.
    /// </summary>
    public static readonly JobKey Key = new(nameof(PostBootTaskRunnerJob), Group);

    private static readonly object _gate = new();
    private static readonly List<PendingTask> _pending = [];
    private static bool _locked;
    private static int _runCount;


    /// <summary>
    /// Adds a task to run once the service container is live.
    /// </summary>
    /// <param name="task">The task to run.</param>
    /// <exception cref="InvalidOperationException">The runner has already started.</exception>
    public static void QueueTask(PostBootTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentException.ThrowIfNullOrWhiteSpace(task.Name);
        ArgumentNullException.ThrowIfNull(task.Callback);

        lock (_gate)
        {
            if (_locked)
                throw new InvalidOperationException(
                    $"Post-boot task '{task.Name}' was queued after the runner started. Queue post-boot tasks before the scheduler starts.");

            _pending.Add(new PendingTask(task));
        }
    }

    /// <summary>
    /// The "Build Schedule" post-boot task: rebuilds the schedule of every <see cref="IOosJob"/> through
    /// <see cref="IJobScheduleService.RebuildAsync"/>.
    /// </summary>
    /// <remarks>
    /// The result is unsuccessful if any job failed to register, so the runner retries the whole rebuild.
    /// Post-boot callbacks take no cancellation token, so the rebuild runs uncancelled.
    /// </remarks>
    /// <param name="serviceProvider">The task's scoped service provider.</param>
    /// <returns>A successful result when every job registered.</returns>
    public static async Task<PostBootTaskResult> InitializeQuartzTaskSchedule(IServiceProvider serviceProvider)
    {
        var result = await serviceProvider.GetRequiredService<IJobScheduleService>().RebuildAsync();
        return new PostBootTaskResult(result.Succeeded, nameof(InitializeQuartzTaskSchedule), result.ToString());
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<PostBootTaskRunnerJob> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PostBootTaskRunnerJob"/>.
    /// </summary>
    public PostBootTaskRunnerJob(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        ILogger<PostBootTaskRunnerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>
    /// Runs every task still queued, then deletes this job once none are left. Stops the app instead when
    /// a required task is given up on.
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        List<PendingTask> tasks;
        int run;
        lock (_gate)
        {
            if (!_locked)
            {
                _locked = true;
                // OrderByDescending is stable, so equal priorities keep their queue order.
                var ordered = _pending.OrderByDescending(p => p.Task.Priority).ToList();
                _pending.Clear();
                _pending.AddRange(ordered);
            }

            tasks = [.. _pending];
            run = ++_runCount;
        }

        foreach (var pending in tasks)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return;

            var task = pending.Task;
            pending.Attempts++;
            var result = await RunAsync(task);

            if (result.Success)
            {
                lock (_gate)
                    _pending.Remove(pending);
                _logger.LogInformation("Post-boot task '{Task}' succeeded on attempt {Attempt}. {Message}",
                    task.Name, pending.Attempts, result.Message);
                continue;
            }

            if (pending.Attempts <= MAX_RETRIES)
            {
                _logger.LogWarning(
                    "Post-boot task '{Task}' failed on attempt {Attempt} of {MaxAttempts} and will be retried. {Message}",
                    task.Name, pending.Attempts, MAX_RETRIES + 1, result.Message);
                continue;
            }

            lock (_gate)
                _pending.Remove(pending);

            if (!task.Required)
            {
                _logger.LogError(
                    "Post-boot task '{Task}' failed {Attempts} times and has been given up on. OOS will keep running without it. {Message}",
                    task.Name, pending.Attempts, result.Message);
                continue;
            }

            _logger.LogCritical(
                "REQUIRED post-boot task '{Task}' failed {Attempts} times. OOS cannot run without it and is shutting down with exit code {ExitCode}. {Message}",
                task.Name, pending.Attempts, REQUIRED_TASK_FAILED_EXIT_CODE, result.Message);
            Environment.ExitCode = REQUIRED_TASK_FAILED_EXIT_CODE;
            _lifetime.StopApplication();
            return;
        }

        int remaining;
        lock (_gate)
            remaining = _pending.Count;

        if (remaining > 0)
            return;

        _logger.LogInformation("Post-boot tasks finished after {Runs} run(s). Removing the post-boot task runner.", run);
        await context.Scheduler.DeleteJob(context.JobDetail.Key, context.CancellationToken);
    }

    /// <summary>
    /// Runs one task in a scope of its own, turning an exception into a failed result.
    /// </summary>
    private async Task<PostBootTaskResult> RunAsync(PostBootTask task)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            return await task.Callback(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-boot task '{Task}' threw.", task.Name);
            return new PostBootTaskResult(false, task.Name, ex.Message);
        }
    }

    /// <summary>
    /// A queued task and how many times it has been attempted.
    /// </summary>
    private sealed class PendingTask(PostBootTask task)
    {
        public PostBootTask Task { get; } = task;
        public int Attempts { get; set; }
    }
}

/// <summary>
/// A task queued to run once the service container is live.
/// </summary>
public record PostBootTask
{
    /// <summary>
    /// Name of the task, used in logs.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The task. It receives the service provider of a scope created for that run alone.
    /// </summary>
    public required Func<IServiceProvider, Task<PostBootTaskResult>> Callback { get; init; }

    /// <summary>
    /// Tasks with a higher priority run first. Defaults to <c>0</c>.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Whether OOS can run without this task.
    /// </summary>
    /// <value>
    /// <see langword="true"/> stops the app when the task is given up on. <see langword="false"/>, the
    /// default, logs an error and keeps running.
    /// </value>
    public bool Required { get; init; }
}

/// <summary>
/// The outcome of one run of a post-boot task.
/// </summary>
/// <param name="Success">
/// <see langword="true"/> when the task is done. <see langword="false"/> keeps the task queued to run again.
/// </param>
/// <param name="Name">Name of the task.</param>
/// <param name="Message">Optional detail for the log.</param>
public record PostBootTaskResult(bool Success, string Name, string? Message);
