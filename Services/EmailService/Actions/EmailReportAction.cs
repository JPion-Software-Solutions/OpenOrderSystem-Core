using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService.Actions;

/// <summary>
/// Service action that renders a report from an email template and sends it to a list of recipients.
/// </summary>
/// <remarks>
/// Runs entirely on the server and never uses the device job queue. Typically triggered by a
/// scheduled event, though it does not know or care whether the event came from a user or a clock. The
/// rule chooses the template and recipients; the report data comes from the event.
/// </remarks>
[RuleAction("email.report", RuleActionKind.Service,
    DisplayName = "Email Report",
    Description = "Renders a report from an email template and sends it to the listed recipients.",
    SettingsType = typeof(EmailReportSettings))]
public class EmailReportAction : IRuleActionHandler
{
    /// <inheritdoc />
    public Task<RuleActionResult> InvokeAsync(RuleActionContext context, CancellationToken ct)
    {
        if (!RuleActionSettings.TryReadSettings(context, out EmailReportSettings? settings, out var error))
            return Task.FromResult(RuleActionResult.Fail(error));

        // TODO: once event types exist, read the event with RuleActionSettings.TryReadEvent. Needs the
        // report data (reportData) the template renders.
        // TODO: render the report with settings.TemplateName and send it to settings.Recipients. Pass
        // context.ActionId to the mail provider as a dedupe key where it supports one, so a retried action
        // does not send the email twice.
        return Task.FromResult(RuleActionResult.Ok());
    }
}

/// <summary>
/// Settings for <see cref="EmailReportAction"/>.
/// </summary>
/// <param name="TemplateName">The email template used to render the report.</param>
/// <param name="Recipients">The addresses the report is sent to. A fixed list chosen by the rule.</param>
public sealed record EmailReportSettings(string TemplateName, string[] Recipients) : IValidatableSettings
{
    /// <inheritdoc />
    public IReadOnlyList<SettingsProblem> Validate()
    {
        var problems = new List<SettingsProblem>();

        if (string.IsNullOrWhiteSpace(TemplateName))
            problems.Add(new("templateName", "Must not be blank."));

        if (Recipients.Length == 0)
            problems.Add(new("recipients", "List at least one recipient."));

        for (var i = 0; i < Recipients.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(Recipients[i]))
                problems.Add(new($"recipients[{i}]", "Must not be blank."));
        }

        return problems;
    }
}
