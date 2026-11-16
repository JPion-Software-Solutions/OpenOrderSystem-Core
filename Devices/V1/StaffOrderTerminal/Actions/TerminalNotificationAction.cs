using OpenOrderSystem.Core.Services.Devices.JobQueue.Interfaces;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1.StaffOrderTerminal.Actions;

/// <summary>
/// Device action that queues a notification for a staff order terminal to show.
/// </summary>
[RuleAction("terminal.notification", RuleActionKind.Device,
    DisplayName = "Terminal Notification",
    Description = "Shows a notification on a staff order terminal.",
    TargetDeviceType = typeof(StaffOrderTerminalDeviceType),
    SettingsType = typeof(TerminalNotificationSettings))]
public class TerminalNotificationAction(IDeviceJobQueue jobQueue) : IRuleActionHandler
{
    private readonly IDeviceJobQueue _jobQueue = jobQueue;

    /// <inheritdoc />
    public Task<RuleActionResult> InvokeAsync(RuleActionContext context, CancellationToken ct)
    {
        if (context.TargetDeviceId is null)
            return Task.FromResult(RuleActionResult.Fail($"Action \"{context.ActionKey}\" needs a target terminal."));

        if (!RuleActionSettings.TryReadSettings(context, out TerminalNotificationSettings? settings, out var error))
            return Task.FromResult(RuleActionResult.Fail(error));

        // TODO: once event types exist, read the event with RuleActionSettings.TryReadEvent if the
        // notification should reference what happened, such as the order ID or order number.
        // TODO: build the notification job from settings.Message and enqueue it with
        // _jobQueue.EnqueueAsync. Set the job's SourceActionId to context.ActionId so a retried action
        // returns the existing job.
        return Task.FromResult(RuleActionResult.Ok());
    }
}

/// <summary>
/// Settings for <see cref="TerminalNotificationAction"/>.
/// </summary>
/// <param name="Message">The notification text shown on the terminal.</param>
public sealed record TerminalNotificationSettings(string Message) : IValidatableSettings
{
    /// <inheritdoc />
    public IReadOnlyList<SettingsProblem> Validate() =>
        string.IsNullOrWhiteSpace(Message) ? [new("message", "Must not be blank.")] : [];
}
