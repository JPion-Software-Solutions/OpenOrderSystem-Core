using OpenOrderSystem.Core.Services.EmailService.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService;

/// <summary>
/// Represents a plain text email form with no template rendering.
/// Body content is set directly as a string and transmitted as-is.
/// Prefer <see cref="HtmlEmailForm"/> for most OOS outbound messaging;
/// this form is provided as a fallback for contexts where HTML email
/// is unsuitable or unsupported by the recipient.
/// </summary>
public class PlaintextEmailForm : IEmailForm
{
    /// <summary>Gets or sets the sender's email address.</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender's display name. When set, the service layer should compose
    /// the From header as <c>Display Name &lt;address@domain.com&gt;</c>.
    /// May be left empty to use the bare address.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipient's email address.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>Gets or sets the CC email address. May be left empty.</summary>
    public string Cc { get; set; } = string.Empty;

    /// <summary>Gets or sets the BCC email address. May be left empty.</summary>
    public string Bcc { get; set; } = string.Empty;

    /// <summary>Gets or sets the email subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the plain text body content of the email.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets a value indicating that this form sends plain text email.</summary>
    public bool IsHtml => false;
}