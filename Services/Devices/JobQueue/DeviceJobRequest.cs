using System.Text.Json;

namespace OpenOrderSystem.Core.Services.Devices.JobQueue;

/// <summary>
/// A job a device action asks the device job queue to hold for a device.
/// </summary>
/// <remarks>
/// Built by a <see cref="EventBus.RuleActionKind.Device"/> action handler and passed to
/// <see cref="Interfaces.IDeviceJobQueue.EnqueueAsync"/>. The device claims the job from the queue
/// and executes it, or passes it to its child devices. The handler does not care which.
/// </remarks>
public sealed record DeviceJobRequest
{
    /// <summary>
    /// The rule action that produced this job. Serves as the idempotency key: enqueueing again with the
    /// same value returns the existing job instead of creating a new one.
    /// </summary>
    /// <remarks>Set from <see cref="EventBus.RuleActionContext.ActionId"/>.</remarks>
    public required Guid SourceActionId { get; init; }

    /// <summary>
    /// The namespaced job type the device uses to decide how to execute the job, such as
    /// <c>print.ticket</c>.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The schema version of <see cref="Payload"/>, so devices can read older or newer payloads for
    /// the same <see cref="Type"/>.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// The device the job is for. May be a dependent device; the queue resolves the owning principal
    /// device when the job is claimed.
    /// </summary>
    public required Guid TargetDeviceId { get; init; }

    /// <summary>
    /// The job's instructions, in the shape <see cref="Type"/> and <see cref="Version"/> define.
    /// </summary>
    public required JsonElement Payload { get; init; }

    /// <summary>
    /// When the job is no longer worth executing, such as a ticket for an order that has long since been
    /// picked up. <see langword="null"/> means it never expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
