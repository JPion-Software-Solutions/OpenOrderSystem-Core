namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// What a rule action does when the action worker runs it.
/// </summary>
public enum RuleActionKind
{
    /// <summary>
    /// Builds a job and enqueues it on the device job queue for a target device. The handler does
    /// not care how the device executes the job.
    /// </summary>
    Device,

    /// <summary>
    /// Performs server-side work directly, such as sending an email. Never uses the device job queue.
    /// </summary>
    Service,
}
