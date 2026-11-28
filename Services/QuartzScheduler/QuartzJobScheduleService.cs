using System.Reflection;
using System.Text.Json;
using OpenOrderSystem.Core.Services.__BuilderExtensions;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;
using OpenOrderSystem.Core.Services.QuartzScheduler.Manifest;
using Quartz;
using Quartz.Impl.Matchers;

namespace OpenOrderSystem.Core.Services.QuartzScheduler;

/// <summary>
/// Quartz implementation of <see cref="IJobScheduleService"/>, with the manifest stored as
/// <c>schedule.json</c> in the data root's config directory.
/// </summary>
/// <remarks>
/// <para>
/// Register as a singleton. The locks that keep rebuilds and saves from overlapping belong to the instance,
/// so a second instance would let them overlap.
/// </para>
/// <para>
/// Each rebuild runs in a DI scope of its own, created here, so callers never have to supply one and a
/// rebuild never picks up a caller's half-used scoped services. Job types and their keys are found by
/// scanning once, on first use; they cannot change while OOS runs.
/// </para>
/// </remarks>
public class QuartzJobScheduleService : IJobScheduleService
{
    /// <summary>The manifest's file name in the config directory.</summary>
    public const string MANIFEST_FILE_NAME = "schedule.json";

    private static readonly MethodInfo _registerOne = typeof(QuartzJobScheduleService)
        .GetMethod(nameof(RegisterOneAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuartzJobScheduleService> _logger;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly Lazy<IReadOnlyList<Type>> _jobTypes = new(FindOosJobTypes);
    private readonly Lazy<JobKeyMap> _jobKeys = new(FindJobKeys);
    private volatile ScheduleManifest _appliedManifest = ScheduleManifest.Empty;

    /// <summary>
    /// Initializes a new instance of <see cref="QuartzJobScheduleService"/>.
    /// </summary>
    /// <param name="schedulerFactory">Gives the running scheduler. Quartz does not register <see cref="IScheduler"/> itself.</param>
    /// <param name="scopeFactory">Creates the DI scope each rebuild runs in.</param>
    /// <param name="logger">The logger.</param>
    public QuartzJobScheduleService(
        ISchedulerFactory schedulerFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<QuartzJobScheduleService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>The full path of the manifest file.</summary>
    public static string ManifestPath => Path.Combine(Bootstrapper.Configuration.ConfigurationDirectory, MANIFEST_FILE_NAME);

    /// <inheritdoc/>
    public ScheduleManifest AppliedManifest => _appliedManifest;

    /// <inheritdoc/>
    public async Task<JobScheduleRebuildResult> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var groupMatcher = GroupMatcher<JobKey>.GroupEquals(IOosJob.Group);

        await _rebuildLock.WaitAsync(cancellationToken);
        try
        {
            _appliedManifest = await LoadManifestForRebuildAsync(cancellationToken);
            LogManifestProblems(_appliedManifest);

            var existing = await scheduler.GetJobKeys(groupMatcher, cancellationToken);
            if (existing.Count > 0)
                await scheduler.DeleteJobs(existing, cancellationToken);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var jobTypes = _jobTypes.Value;
            var failures = new List<JobRegistrationFailure>();
            foreach (var type in jobTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var register = (Task)_registerOne.MakeGenericMethod(type)
                        .Invoke(null, [scope.ServiceProvider, scheduler, cancellationToken])!;
                    await register;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    var cause = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
                    _logger.LogError(cause, "Job '{Job}' could not be registered.", AttributedTypeScanner.Describe(type));
                    failures.Add(new JobRegistrationFailure(type, cause.Message));
                }
            }

            var scheduled = (await scheduler.GetJobKeys(groupMatcher, cancellationToken)).Count;
            var result = new JobScheduleRebuildResult(jobTypes.Count, scheduled, failures);
            if (result.Succeeded)
                _logger.LogInformation("Schedule rebuilt. {Summary}", result);
            else
                _logger.LogWarning("Schedule rebuilt with failures. {Summary}", result);

            return result;
        }
        finally
        {
            _rebuildLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<ScheduleManifest> ReadManifestAsync(CancellationToken cancellationToken = default)
    {
        var path = ManifestPath;
        if (!File.Exists(path))
            return ScheduleManifest.Empty;

        await using var stream = File.OpenRead(path);
        var manifest = await JsonSerializer.DeserializeAsync<ScheduleManifest>(
                           stream, ManifestJobContextExtensions.SerializerOptions, cancellationToken)
                       ?? throw new InvalidDataException($"The schedule manifest '{path}' is empty.");

        if (manifest.Version != ScheduleManifest.CurrentVersion)
            throw new InvalidDataException(
                $"The schedule manifest '{path}' is version {manifest.Version}; this build reads version {ScheduleManifest.CurrentVersion}.");

        return manifest;
    }

    /// <inheritdoc/>
    public async Task<JobScheduleRebuildResult> SaveManifestAsync(ScheduleManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var path = ManifestPath;
        var tempPath = path + ".tmp";

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, ManifestJobContextExtensions.SerializerOptions, cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true);
            _logger.LogInformation("Schedule manifest saved to '{Path}'.", path);
        }
        finally
        {
            _fileLock.Release();
        }

        return await RebuildAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ScheduleFromManifestAsync<TJob>(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken)
        where TJob : IOosJob
    {
        var keys = _jobKeys.Value;
        var jobName = AttributedTypeScanner.Describe(typeof(TJob));

        if (!keys.KeyByType.TryGetValue(typeof(TJob), out var key))
        {
            _logger.LogError(
                "Job '{Job}' schedules from the manifest but has no [OosQuartzJob] key, so it cannot be found there. It was not scheduled.",
                jobName);
            return 0;
        }

        if (!keys.UniqueKeys.Contains(key))
        {
            _logger.LogError(
                "Job '{Job}' was not scheduled: its key '{Key}' is declared by more than one job, so its manifest entry is ambiguous.",
                jobName, key);
            return 0;
        }

        if (!_appliedManifest.Jobs.TryGetValue(key, out var entry) || entry is null)
        {
            _logger.LogDebug("Job '{Key}' is not in the schedule manifest, so it was not scheduled.", key);
            return 0;
        }

        if (!entry.Enabled)
        {
            _logger.LogInformation("Job '{Key}' is disabled in the schedule manifest.", key);
            return 0;
        }

        var timeZone = await GetStoreTimeZoneAsync(services);
        var count = 0;
        foreach (var (instanceId, instance) in entry.Instances ?? new Dictionary<string, ScheduleManifestInstance>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                _logger.LogWarning("Job '{Key}' has an instance with a blank ID in the schedule manifest. It was skipped.", key);
                continue;
            }

            if (instance is null || !instance.Enabled)
                continue;

            if (string.IsNullOrWhiteSpace(instance.Cron) || !CronExpression.IsValidExpression(instance.Cron))
            {
                _logger.LogWarning(
                    "Instance '{Key}:{Instance}' has a missing or invalid cron expression ('{Cron}'). It was skipped.",
                    key, instanceId, instance.Cron);
                continue;
            }

            var identity = $"{key}:{instanceId}";
            var job = JobBuilder.Create<TJob>()
                .WithIdentity(identity, IOosJob.Group)
                .UsingJobData(ManifestJobContextExtensions.InstanceIdDataKey, instanceId)
                .UsingJobData(ManifestJobContextExtensions.SettingsDataKey, instance.Settings?.GetRawText() ?? string.Empty)
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{identity}-trigger", IOosJob.Group)
                .WithCronSchedule(instance.Cron, c => c
                    .InTimeZone(timeZone)
                    .WithMisfireHandlingInstructionFireAndProceed())
                .Build();

            await scheduler.ScheduleJob(job, trigger, cancellationToken);
            count++;
        }

        _logger.LogInformation("Job '{Key}': {Count} instance(s) scheduled from the manifest.", key, count);
        return count;
    }

    /// <summary>
    /// Reads the manifest for a rebuild. A missing file gives an empty manifest; an unreadable one is logged
    /// and also gives an empty manifest, so the always-on jobs are still scheduled.
    /// </summary>
    private async Task<ScheduleManifest> LoadManifestForRebuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            var manifest = await ReadManifestAsync(cancellationToken);
            if (ReferenceEquals(manifest, ScheduleManifest.Empty))
                _logger.LogInformation("No schedule manifest at '{Path}'. Only always-on jobs will be scheduled.", ManifestPath);
            return manifest;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "The schedule manifest at '{Path}' could not be read. Only always-on jobs will be scheduled until it is fixed and the schedule is rebuilt.",
                ManifestPath);
            return ScheduleManifest.Empty;
        }
    }

    /// <summary>
    /// Logs problems with job keys found by the scan, and manifest entries that match no job.
    /// </summary>
    private void LogManifestProblems(ScheduleManifest manifest)
    {
        var keys = _jobKeys.Value;
        foreach (var error in keys.Errors)
            _logger.LogError("Scheduled job key problem: {Error}", error);

        foreach (var key in (manifest.Jobs ?? new Dictionary<string, ScheduleManifestJob>()).Keys)
        {
            if (!keys.KeyByType.Values.Contains(key, StringComparer.Ordinal))
                _logger.LogWarning("The schedule manifest lists job '{Key}', but no job has that key. The entry was ignored.", key);
        }
    }

    /// <summary>
    /// The store's configured timezone, or UTC when it cannot be resolved.
    /// </summary>
    private async Task<TimeZoneInfo> GetStoreTimeZoneAsync(IServiceProvider services)
    {
        string? id = null;
        try
        {
            id = await services.GetRequiredService<ITimeNormalizationService>().GetLocalTimezoneId();
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The store timezone ('{TimeZone}') could not be resolved. Manifest jobs are scheduled in UTC.", id);
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// The keys of every <see cref="IOosJob"/> carrying <see cref="OosQuartzJobAttribute"/>, and which keys
    /// are declared by exactly one job.
    /// </summary>
    private static JobKeyMap FindJobKeys()
    {
        var errors = new List<string>();
        var found = AttributedTypeScanner.FindAttributed<OosQuartzJobAttribute>(typeof(IOosJob), a => a.Key, "Scheduled job", errors);

        var keyByType = found
            .Where(f => typeof(IOosJob).IsAssignableFrom(f.Type) && !f.Type.IsGenericTypeDefinition
                        && !string.IsNullOrWhiteSpace(f.Attribute.Key))
            .ToDictionary(f => f.Type, f => f.Attribute.Key);

        var uniqueKeys = keyByType.Values
            .GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new JobKeyMap(keyByType, uniqueKeys, errors);
    }

    /// <summary>
    /// Every non-abstract, closed class implementing <see cref="IOosJob"/> in the assemblies that can
    /// declare one, in name order.
    /// </summary>
    private static IReadOnlyList<Type> FindOosJobTypes() =>
        AttributedTypeScanner.CandidateAssemblies(typeof(IOosJob).Assembly)
            .SelectMany(LoadableTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                        && typeof(IOosJob).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// An assembly's types, skipping any that cannot be loaded.
    /// </summary>
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>
    /// Calls <see cref="IOosJob.RegisterAsync"/> on <typeparamref name="TJob"/>. Static abstract members
    /// cannot be called from a <see cref="Type"/> found at runtime, so the scan closes this method over each
    /// job type instead.
    /// </summary>
    private static Task RegisterOneAsync<TJob>(IServiceProvider services, IScheduler scheduler, CancellationToken cancellationToken)
        where TJob : IOosJob
        => TJob.RegisterAsync(services, scheduler, cancellationToken);

    /// <param name="KeyByType">Each keyed job type and its key.</param>
    /// <param name="UniqueKeys">The keys declared by exactly one job type.</param>
    /// <param name="Errors">Problems the scan found, logged on each rebuild.</param>
    private sealed record JobKeyMap(
        IReadOnlyDictionary<Type, string> KeyByType,
        IReadOnlySet<string> UniqueKeys,
        IReadOnlyList<string> Errors);
}
