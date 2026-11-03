using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using OpenOrderSystem.Core.Services.UI.Notification.Interface;
using NotificationRecord = OpenOrderSystem.Core.Services.UI.Notification.Interface.INotificationService.Notification;

namespace OpenOrderSystem.Core.Services.UI.Notification;

/// <summary>
/// <see cref="INotificationService"/> backed by a JSON file. Register as a singleton: the
/// instance owns the file, and a second instance on the same path would overwrite the first's
/// writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writes</b> go through a queue drained by a single writer task, so callers never block
/// each other on a lock. The writer takes every queued request, applies them in order to a copy
/// of the state, prunes, and persists once (temp file, flush to disk, atomic rename). Only then
/// does it publish the new state and complete the callers' tasks. If persisting fails after
/// retries, every request in that batch faults with <see cref="NotificationPersistenceException"/>
/// and the in-memory state is left as it was, so nothing is acknowledged that is not on disk.
/// </para>
/// <para>
/// <b>Reads</b> use an immutable snapshot and never wait on the queue. A caller that awaited a
/// write sees it in later reads.
/// </para>
/// <para>
/// <b>Pruning:</b> answered notifications older than the retention period (90 days by default)
/// are dropped at load and with each write batch. Open notifications are never pruned.
/// </para>
/// <para>
/// <b>Corrupt history:</b> an unreadable file is moved aside (<c>*.corrupt-yyyyMMddHHmmss</c>),
/// the service starts empty, and a critical condition is raised to <c>system.admin</c>.
/// </para>
/// <para>
/// Dispose to drain the queue on shutdown; the DI container does this for singletons.
/// </para>
/// </remarks>
public sealed class JsonNotificationService : INotificationService, IAsyncDisposable, IDisposable
{
    public const string HistoryFileName = "NotificationHistory.json";
    public const string CorruptHistorySourceKey = "notification.history-corrupt";
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(90);

    private const int FileFormatVersion = 1;
    private const int MaxSourceKeyLength = 200;
    private static readonly TimeSpan[] PersistRetryDelays = [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250)];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly TimeSpan _retention;
    private readonly TimeProvider _time;
    private readonly ILogger<JsonNotificationService> _logger;
    private readonly Channel<PendingWrite> _queue =
        Channel.CreateUnbounded<PendingWrite>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writer;

    private ImmutableDictionary<Guid, NotificationRecord> _state;

    /// <param name="historyFilePath">Where to keep the history. The directory is created if needed.</param>
    /// <param name="logger">Logger. Notification ids and targets are logged, never content.</param>
    /// <param name="timeProvider">Clock; defaults to the system clock.</param>
    /// <param name="retention">How long answered notifications are kept; defaults to <see cref="DefaultRetention"/>.</param>
    public JsonNotificationService(
        string historyFilePath,
        ILogger<JsonNotificationService> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? retention = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyFilePath);
        _path = Path.GetFullPath(historyFilePath);
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _retention = retention ?? DefaultRetention;

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var corrupt = false;
        _state = Prune(Load(ref corrupt), _time.GetUtcNow());
        _writer = Task.Run(WriterLoopAsync);

        if (corrupt)
            _ = RaiseConditionAsync(CorruptHistorySourceKey, NotificationTarget.Admin,
                    "The notification history file could not be read and was moved aside. Earlier notifications " +
                    "are not shown. Check the data root for a NotificationHistory.json.corrupt-* file.",
                    NotificationSeverity.Critical)
                .ContinueWith(t => _logger.LogError(t.Exception, "Could not record the corrupt-history notification."),
                    TaskContinuationOptions.OnlyOnFaulted);
    }

    // ---------------------------------------------------------------- reads

    public Task<ICollection<NotificationRecord>> GetNotificationsAsync(string target)
    {
        var normalized = NotificationTarget.Normalize(target);
        return Task.FromResult(Select(n => n.Target == normalized));
    }

    public Task<ICollection<NotificationRecord>> GetNotificationsForRecipientAsync(
        string surface, string? user = null, IEnumerable<string>? groups = null)
    {
        var parsed = NotificationTarget.Parse(surface, nameof(surface));
        if (parsed.Kind != NotificationRecipientKind.Surface)
            throw new ArgumentException($"'{surface}' names a recipient; pass the surface only.", nameof(surface));

        var targets = new HashSet<string>(StringComparer.Ordinal) { parsed.Surface };
        if (!string.IsNullOrEmpty(user))
            targets.Add(NotificationTarget.ForUser(parsed.Surface, user));
        foreach (var group in groups ?? [])
            targets.Add(NotificationTarget.ForGroup(parsed.Surface, group));

        return Task.FromResult(Select(n => targets.Contains(n.Target)));
    }

    public Task<NotificationRecord?> GetNotificationAsync(Guid notificationId)
        => Task.FromResult(Volatile.Read(ref _state).GetValueOrDefault(notificationId));

    private ICollection<NotificationRecord> Select(Func<NotificationRecord, bool> predicate)
        => Volatile.Read(ref _state).Values
            .Where(predicate)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ToList();

    // ---------------------------------------------------------------- writes

    public Task<NotificationResult> PostNotificationAsync(string target, string content, NotificationSeverity severity)
    {
        var normalized = NotificationTarget.Normalize(target);
        ValidateContent(content);
        ValidateSeverity(severity);

        return Enqueue((state, now) =>
        {
            var n = new NotificationRecord(Guid.CreateVersion7(now), normalized, content, severity, now, null, null);
            return (state.Add(n.Id, n), new NotificationResult(n.Id, NotificationResultState.Created));
        });
    }

    public Task<NotificationResult> RaiseConditionAsync(
        string sourceKey, string target, string content, NotificationSeverity severity)
    {
        ValidateSourceKey(sourceKey);
        var normalized = NotificationTarget.Normalize(target);
        ValidateContent(content);
        ValidateSeverity(severity);

        return Enqueue((state, now) =>
        {
            var existing = FindOpenCondition(state, sourceKey);
            if (existing is null)
            {
                var n = new NotificationRecord(Guid.CreateVersion7(now), normalized, content, severity, now, null, null, sourceKey);
                return (state.Add(n.Id, n), new NotificationResult(n.Id, NotificationResultState.Created));
            }

            if (existing.Target == normalized && existing.Content == content && existing.Severity == severity)
                return (state, new NotificationResult(existing.Id, NotificationResultState.Unchanged));

            var updated = existing with
            {
                Target = normalized,
                Content = content,
                Severity = severity,
                // Escalation or a new audience means the alert has not been presented in this form yet.
                AlertedAt = severity > existing.Severity || existing.Target != normalized ? null : existing.AlertedAt
            };
            return (state.SetItem(updated.Id, updated), new NotificationResult(updated.Id, NotificationResultState.Updated));
        });
    }

    public Task<NotificationResult> ResolveConditionAsync(string sourceKey)
    {
        ValidateSourceKey(sourceKey);

        return Enqueue((state, now) =>
        {
            var existing = FindOpenCondition(state, sourceKey);
            if (existing is null)
                return (state, new NotificationResult(Guid.Empty, NotificationResultState.NotFound));

            return (state.SetItem(existing.Id, existing with { AnsweredAt = now }),
                new NotificationResult(existing.Id, NotificationResultState.Updated));
        });
    }

    public Task<NotificationResult> AnswerNotificationAsync(Guid notificationId)
        => Enqueue((state, now) =>
        {
            if (!state.TryGetValue(notificationId, out var n))
                return (state, new NotificationResult(notificationId, NotificationResultState.NotFound));
            if (!n.IsOpen)
                return (state, new NotificationResult(notificationId, NotificationResultState.Unchanged));

            return (state.SetItem(n.Id, n with { AnsweredAt = now }),
                new NotificationResult(notificationId, NotificationResultState.Updated));
        });

    public Task<NotificationResult> ConfirmAlertAsync(Guid notificationId)
        => Enqueue((state, now) =>
        {
            if (!state.TryGetValue(notificationId, out var n))
                return (state, new NotificationResult(notificationId, NotificationResultState.NotFound));
            if (n.AlertedAt is not null)
                return (state, new NotificationResult(notificationId, NotificationResultState.Unchanged));

            return (state.SetItem(n.Id, n with { AlertedAt = now }),
                new NotificationResult(notificationId, NotificationResultState.Updated));
        });

    private static NotificationRecord? FindOpenCondition(ImmutableDictionary<Guid, NotificationRecord> state, string sourceKey)
        => state.Values.FirstOrDefault(n => n.IsOpen && string.Equals(n.SourceKey, sourceKey, StringComparison.Ordinal));

    private static void ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Notification content cannot be empty.", nameof(content));
    }

    private static void ValidateSeverity(NotificationSeverity severity)
    {
        if (!Enum.IsDefined(severity))
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown notification severity.");
    }

    private static void ValidateSourceKey(string sourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        if (sourceKey.Length > MaxSourceKeyLength)
            throw new ArgumentException($"Source keys are limited to {MaxSourceKeyLength} characters.", nameof(sourceKey));
    }

    // ---------------------------------------------------------------- write queue

    private delegate (ImmutableDictionary<Guid, NotificationRecord> State, NotificationResult Result) Mutation(
        ImmutableDictionary<Guid, NotificationRecord> state, DateTimeOffset now);

    private sealed record PendingWrite(Mutation Apply, TaskCompletionSource<NotificationResult> Completion);

    private Task<NotificationResult> Enqueue(Mutation mutation)
    {
        var write = new PendingWrite(mutation,
            new TaskCompletionSource<NotificationResult>(TaskCreationOptions.RunContinuationsAsynchronously));

        if (!_queue.Writer.TryWrite(write))
            throw new ObjectDisposedException(nameof(JsonNotificationService));

        return write.Completion.Task;
    }

    private async Task WriterLoopAsync()
    {
        var reader = _queue.Reader;
        var batch = new List<PendingWrite>();
        var results = new List<(PendingWrite Write, NotificationResult Result)>();

        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            batch.Clear();
            results.Clear();
            while (reader.TryRead(out var write))
                batch.Add(write);

            var original = Volatile.Read(ref _state);
            var working = original;
            var now = _time.GetUtcNow();

            foreach (var write in batch)
            {
                try
                {
                    (working, var result) = write.Apply(working, now);
                    results.Add((write, result));
                }
                catch (Exception ex)
                {
                    // Validation happens before enqueueing, so this is a bug; fail only this request.
                    _logger.LogError(ex, "Notification write failed while applying.");
                    write.Completion.TrySetException(ex);
                }
            }

            working = Prune(working, now);

            if (!ReferenceEquals(working, original))
            {
                try
                {
                    await PersistWithRetryAsync(working).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Nothing in this batch is on disk, so none of it is published or acknowledged.
                    _logger.LogError(ex, "Could not persist {Count} notification write(s) to {Path}.", results.Count, _path);
                    var failure = new NotificationPersistenceException(_path, ex);
                    foreach (var (write, _) in results)
                        write.Completion.TrySetException(failure);
                    continue;
                }

                Volatile.Write(ref _state, working);
            }

            foreach (var (write, result) in results)
            {
                if (result.State == NotificationResultState.Created)
                    _logger.LogInformation("Notification {Id} posted to {Target}.", result.Id, working[result.Id].Target);
                write.Completion.TrySetResult(result);
            }
        }
    }

    private ImmutableDictionary<Guid, NotificationRecord> Prune(ImmutableDictionary<Guid, NotificationRecord> state, DateTimeOffset now)
    {
        var cutoff = now - _retention;
        var expired = state.Values.Where(n => n.AnsweredAt < cutoff).Select(n => n.Id).ToList();
        return expired.Count == 0 ? state : state.RemoveRange(expired);
    }

    // ---------------------------------------------------------------- persistence

    private sealed record HistoryFile(int Version, List<NotificationRecord> Notifications);

    private ImmutableDictionary<Guid, NotificationRecord> Load(ref bool corrupt)
    {
        if (!File.Exists(_path))
            return ImmutableDictionary<Guid, NotificationRecord>.Empty;

        try
        {
            using var stream = File.OpenRead(_path);
            var file = JsonSerializer.Deserialize<HistoryFile>(stream, JsonOptions)
                       ?? throw new JsonException("History file is empty.");
            if (file.Version > FileFormatVersion)
                throw new InvalidOperationException(
                    $"{_path} has format version {file.Version}; this build understands up to {FileFormatVersion}. " +
                    "Refusing to start rather than overwrite a newer file.");

            return (file.Notifications ?? []).ToImmutableDictionary(n => n.Id);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
        {
            // ArgumentException covers duplicate ids.
            var aside = $"{_path}.corrupt-{_time.GetUtcNow():yyyyMMddHHmmss}";
            File.Move(_path, aside);
            _logger.LogError(ex, "Notification history at {Path} is unreadable; moved to {Aside} and starting empty.", _path, aside);
            corrupt = true;
            return ImmutableDictionary<Guid, NotificationRecord>.Empty;
        }
    }

    private async Task PersistWithRetryAsync(ImmutableDictionary<Guid, NotificationRecord> state)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await PersistAsync(state).ConfigureAwait(false);
                return;
            }
            catch (IOException ex) when (attempt < PersistRetryDelays.Length)
            {
                _logger.LogWarning(ex, "Notification history write failed (attempt {Attempt}); retrying.", attempt + 1);
                await Task.Delay(PersistRetryDelays[attempt], _time).ConfigureAwait(false);
            }
        }
    }

    private async Task PersistAsync(ImmutableDictionary<Guid, NotificationRecord> state)
    {
        var file = new HistoryFile(FileFormatVersion, state.Values.OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToList());
        var temp = _path + ".tmp";

        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, file, JsonOptions).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, _path, overwrite: true);
    }

    // ---------------------------------------------------------------- shutdown

    /// <summary>Stops accepting writes and waits for queued ones to be persisted.</summary>
    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _writer.ConfigureAwait(false);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}

/// <summary>
/// A notification write could not be saved. The write did not take effect; the caller may retry.
/// </summary>
public sealed class NotificationPersistenceException(string path, Exception inner)
    : IOException($"Notification history could not be saved to '{path}'. The change was not applied.", inner);
