using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService.Actions;

/// <summary>
/// Service action that renders an order email, such as a confirmation or receipt, from an email
/// template and sends it to the order's customer.
/// </summary>
/// <remarks>
/// Runs entirely on the server and never uses the device job queue. Typically triggered by an order
/// event such as <c>order.placed</c>. The rule only chooses the template; the order data and the
/// customer's email address come from the event.
/// </remarks>
[RuleAction("email.order", RuleActionKind.Service,
    DisplayName = "Email Order",
    Description = "Renders an order email, such as a confirmation or receipt, from an email template and sends it to the order's customer.",
    SettingsType = typeof(EmailOrderSettings))]
public class EmailOrderAction : IRuleActionHandler
{
    /// <inheritdoc />
    public Task<RuleActionResult> InvokeAsync(RuleActionContext context, CancellationToken ct)
    {
        if (!RuleActionSettings.TryReadSettings(context, out EmailOrderSettings? settings, out var error))
            return Task.FromResult(RuleActionResult.Fail(error));

        // TODO: once event types exist, read the event with RuleActionSettings.TryReadEvent. Needs the
        // order data (orderData) the template renders and the customer's email address; fail permanently
        // if the address is missing or blank.
        // TODO: render the order email with settings.TemplateName and send it. Pass context.ActionId to the
        // mail provider as a dedupe key where it supports one, so a retried action does not send the email
        // twice.
        return Task.FromResult(RuleActionResult.Ok());
    }
}

/// <summary>
/// Settings for <see cref="EmailOrderAction"/>.
/// </summary>
/// <param name="TemplateName">The email template used to render the order email.</param>
public sealed record EmailOrderSettings(string TemplateName) : IValidatableSettings
{
    /// <inheritdoc />
    public IReadOnlyList<SettingsProblem> Validate() =>
        string.IsNullOrWhiteSpace(TemplateName) ? [new("templateName", "Must not be blank.")] : [];
}
