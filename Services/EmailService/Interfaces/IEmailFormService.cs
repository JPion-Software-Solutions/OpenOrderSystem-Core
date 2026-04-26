using OpenOrderSystem.Core.Services.EmailService.Exceptions;

namespace OpenOrderSystem.Core.Services.EmailService.Interfaces;

/// <summary>
/// Defines the contract for a service that resolves email templates and constructs
/// ready-to-send email forms. Implementations are responsible for template resolution,
/// form population, and pre-send validation.
/// </summary>
public interface IEmailFormService
{
    /// <summary>
    /// Resolves an email template by name, walking the configured resolution chain
    /// (user override → built-in) and returning the first match found.
    /// </summary>
    /// <param name="templateName">
    /// The well-known template identifier, e.g. <c>"order-confirmation"</c>.
    /// The implementation is responsible for mapping this key to a file path.
    /// </param>
    /// <returns>The raw HTML template string.</returns>
    /// <exception cref="EmailTemplateNotFoundException">
    /// Thrown when no matching template is found in any resolution layer.
    /// </exception>
    Task<string> GetTemplateAsync(string templateName);

    /// <summary>
    /// Constructs a fully populated <see cref="HtmlEmailForm"/> ready for sending.
    /// </summary>
    /// <param name="template">The raw HTML template string, typically from <see cref="GetTemplateAsync"/>.</param>
    /// <param name="parameters">
    /// A dictionary of token keys and their replacement values.
    /// Keys should match tokens defined in the template using <c>{{token}}</c> syntax.
    /// Use <see cref="HtmlEmailForm.GetDataFrom"/> to populate this from a POCO.
    /// </param>
    /// <returns>A populated <see cref="HtmlEmailForm"/> ready for submission to <see cref="IEmailService"/>.</returns>
    HtmlEmailForm CreateHtmlEmailForm(string template, Dictionary<string, string> parameters);

    /// <summary>
    /// Constructs a <see cref="PlaintextEmailForm"/> from discrete addressing and body parameters.
    /// Use when HTML rendering is unnecessary or unsupported by the recipient.
    /// </summary>
    /// <param name="sender">The sender's email address.</param>
    /// <param name="senderName">The sender's display name. Composed into the From header as <c>Display Name &lt;address@domain.com&gt;</c>.</param>
    /// <param name="recipient">The recipient's email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <param name="body">The plain text body content.</param>
    /// <param name="cc">Optional CC email address.</param>
    /// <param name="bcc">Optional BCC email address.</param>
    /// <returns>A populated <see cref="PlaintextEmailForm"/> ready for submission to <see cref="IEmailService"/>.</returns>
    PlaintextEmailForm CreatePlaintextEmailForm(string sender, string senderName, string recipient, string subject,
        string body, string? cc = null, string? bcc = null);

    /// <summary>
    /// Validates an HTML email template against known email client compatibility rules.
    /// Checks for hard failures that will break delivery and warnings that may cause
    /// rendering issues in specific clients.
    /// </summary>
    /// <param name="template">The raw HTML template string to validate.</param>
    /// <returns>
    /// A <see cref="TemplateValidationReport"/> containing any <see cref="ValidationFinding"/> items found.
    /// Check <see cref="TemplateValidationReport.IsClean"/> for a pass/fail summary.
    /// </returns>
    TemplateValidationReport ValidateTemplate(string template) => EmailTemplateValidator.Validate(template);
}