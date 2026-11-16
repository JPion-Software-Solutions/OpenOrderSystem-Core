namespace OpenOrderSystem.Core.Services.Devices.JobQueue.Interfaces;

/// <summary>
/// The queue of work waiting for devices.
/// </summary>
/// <remarks>
/// Device action handlers enqueue jobs here; devices claim their jobs and execute them, or pass them
/// to their child devices. The queue only ever holds work for devices. Server-side work runs as a
/// service action and never passes through here.
/// </remarks>
public interface IDeviceJobQueue
{
    /// <summary>
    /// Adds a job for a device.
    /// </summary>
    /// <remarks>
    /// Enqueueing is idempotent. If a job with the same <see cref="DeviceJobRequest.SourceActionId"/>
    /// already exists, its ID is returned and no new job is created, so a retried action never
    /// produces a duplicate job.
    /// </remarks>
    /// <param name="request">The job to add.</param>
    /// <param name="ct">Cancels the enqueue.</param>
    /// <returns>The ID of the new job, or of the existing job for the same source action.</returns>
    Task<Guid> EnqueueAsync(DeviceJobRequest request, CancellationToken ct);
}
