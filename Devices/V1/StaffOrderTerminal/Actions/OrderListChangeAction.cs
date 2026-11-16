using OpenOrderSystem.Core.Services.Devices.JobQueue.Interfaces;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1.StaffOrderTerminal.Actions;

/// <summary>
/// Device action that tells a staff order terminal an order in its list has changed.
/// </summary>
/// <remarks>
/// Takes no settings: which order changed is event data, read from the event.
/// </remarks>
[RuleAction("terminal.order-list-change", RuleActionKind.Device,
    DisplayName = "Terminal Order List Change",
    Description = "Tells a staff order terminal that an order in its list was added or changed.",
    TargetDeviceType = typeof(StaffOrderTerminalDeviceType))]
public class OrderListChangeAction(IDeviceJobQueue jobQueue) : IRuleActionHandler
{
    private readonly IDeviceJobQueue _jobQueue = jobQueue;

    /// <inheritdoc />
    public Task<RuleActionResult> InvokeAsync(RuleActionContext context, CancellationToken ct)
    {
        if (context.TargetDeviceId is null)
            return Task.FromResult(RuleActionResult.Fail($"Action \"{context.ActionKey}\" needs a target terminal."));

        // TODO: once event types exist, read the event with RuleActionSettings.TryReadEvent. Needs the
        // order ID (orderId) of the order that was added or changed; fail permanently if it is missing or
        // empty.
        // TODO: build the order list change job and enqueue it with _jobQueue.EnqueueAsync. Set the job's
        // SourceActionId to context.ActionId so a retried action returns the existing job.
        return Task.FromResult(RuleActionResult.Ok());
    }
}
