using OpenOrderSystem.Core.Services.Devices.JobQueue.Interfaces;
using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Devices.V1.Printer.Actions;

/// <summary>
/// Device action that renders an order ticket from a template and queues it for a printer.
/// </summary>
[RuleAction("print.ticket", RuleActionKind.Device,
    DisplayName = "Print Ticket",
    Description = "Renders an order ticket from a template and queues it for a printer.",
    TargetDeviceType = typeof(PrinterDeviceType),
    SettingsType = typeof(PrintTicketSettings))]
public class PrintTicketAction(IDeviceJobQueue jobQueue) : IRuleActionHandler
{
    private readonly IDeviceJobQueue _jobQueue = jobQueue;

    /// <inheritdoc />
    public Task<RuleActionResult> InvokeAsync(RuleActionContext context, CancellationToken ct)
    {
        if (context.TargetDeviceId is null)
            return Task.FromResult(RuleActionResult.Fail($"Action \"{context.ActionKey}\" needs a target printer."));

        if (!RuleActionSettings.TryReadSettings(context, out PrintTicketSettings? settings, out var error))
            return Task.FromResult(RuleActionResult.Fail(error));

        // TODO: once event types exist, read the event with RuleActionSettings.TryReadEvent. Needs the
        // order ID and the order data (lines, modifiers, totals, customer name, notes) the ticket renders.
        // TODO: render the ticket with settings.TemplateName and enqueue it with _jobQueue.EnqueueAsync. Set
        // the job's SourceActionId to context.ActionId so a retried action returns the existing job.
        return Task.FromResult(RuleActionResult.Ok());
    }
}

/// <summary>
/// Settings for <see cref="PrintTicketAction"/>: how a rule wants its tickets printed.
/// </summary>
/// <param name="TemplateName">The print template used to render the ticket.</param>
public sealed record PrintTicketSettings(string TemplateName) : IValidatableSettings
{
    /// <inheritdoc />
    public IReadOnlyList<SettingsProblem> Validate() =>
        string.IsNullOrWhiteSpace(TemplateName) ? [new("templateName", "Must not be blank.")] : [];
}
