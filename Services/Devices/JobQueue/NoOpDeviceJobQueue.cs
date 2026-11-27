using OpenOrderSystem.Core.Services.Devices.JobQueue.Interfaces;

namespace OpenOrderSystem.Core.Services.Devices.JobQueue;

// TEMPORARY STUB. The real IDeviceJobQueue implementation is being built on the device branch. When the
// two meet in a merge, the real implementation wins every conflict: delete this file and its registration,
// and keep the other side.

/// <summary>
/// A stand-in <see cref="IDeviceJobQueue"/> that stores nothing, so the device rule actions can be
/// constructed before the real queue exists.
/// </summary>
/// <remarks>
/// <para>
/// Every job is dropped. Each enqueue logs a warning naming the job, so a device action that fires is
/// visible in the log rather than lost without a trace.
/// </para>
/// <para>
/// The returned ID is the request's <see cref="DeviceJobRequest.SourceActionId"/>, which keeps the
/// interface's promise that enqueueing the same source action twice returns the same ID.
/// </para>
/// </remarks>
public sealed class NoOpDeviceJobQueue : IDeviceJobQueue
{
    private readonly ILogger<NoOpDeviceJobQueue> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="NoOpDeviceJobQueue"/>.
    /// </summary>
    public NoOpDeviceJobQueue(ILogger<NoOpDeviceJobQueue> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>Drops the job and logs a warning. Nothing is stored.</remarks>
    public Task<Guid> EnqueueAsync(DeviceJobRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogWarning(
            "Device job '{JobType}' v{Version} for device {DeviceId} (action {ActionId}) was dropped: the device job queue is a no-op stub.",
            request.Type, request.Version, request.TargetDeviceId, request.SourceActionId);

        return Task.FromResult(request.SourceActionId);
    }
}
