using OpenOrderSystem.Core.Services.__BuilderExtensions;

namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// Runs one kind of rule action. Each implementation declares its key and kind with
/// <see cref="RuleActionAttribute"/> and is registered as a keyed scoped service by
/// <see cref="EventBusBuilderExtensions.AddOosEventBusServices"/>.
/// </summary>
/// <remarks>
/// <para>
/// A handler works like a manager delegating a task. A <see cref="RuleActionKind.Device"/> action
/// builds a job and enqueues it on the device job queue; it does not care how the device executes
/// it. A <see cref="RuleActionKind.Service"/> action performs one piece of server-side work directly.
/// </para>
/// <para>
/// Execution is at-least-once. A handler must be safe to run more than once for the same
/// <see cref="RuleActionContext.ActionId"/>: use that ID as the idempotency key for anything it
/// produces, so a retry never creates a duplicate job, email or other side effect.
/// </para>
/// </remarks>
public interface IRuleActionHandler
{
    /// <summary>
    /// Runs the action described by <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The queued action, with its event and settings in the input and its target device chosen.</param>
    /// <param name="ct">Cancelled when the worker stops the run, for example on shutdown or timeout.</param>
    /// <returns>
    /// The outcome. Return <see cref="RuleActionResult.Fail"/> for problems retrying cannot fix (such
    /// as invalid settings) and <see cref="RuleActionResult.Retry"/> for transient ones, so the worker
    /// does not retry what will never succeed.
    /// </returns>
    Task<RuleActionResult> InvokeAsync(RuleActionContext context, CancellationToken ct);
}
