namespace OpenOrderSystem.Core.Services.EmailService.Interfaces
{
    /// <summary>
    /// Defines the contract for an email form passed to an email sending service.
    /// Implementations are responsible for providing all addressing, subject, and
    /// body content required to construct and dispatch an outbound email message.
    /// </summary>
    public interface IEmailForm
    {
        /// <summary>Gets the sender's email address.</summary>
        string Sender { get; }

        /// <summary>
        /// Gets the sender's display name. When provided, the service layer should compose
        /// the From header as <c>Display Name &lt;address@domain.com&gt;</c>.
        /// May be empty, in which case the bare address should be used.
        /// </summary>
        string SenderName { get; }

        /// <summary>Gets the recipient's email address.</summary>
        string Recipient { get; }

        /// <summary>Gets the CC email address. May be empty.</summary>
        string Cc { get; }

        /// <summary>Gets the BCC email address. May be empty.</summary>
        string Bcc { get; }

        /// <summary>Gets the email subject line.</summary>
        string Subject { get; }

        /// <summary>
        /// Gets the fully rendered body content of the email, ready for transmission.
        /// The format of this content should correspond to <see cref="IsHtml"/>.
        /// </summary>
        string Body { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Body"/> contains HTML content.
        /// The email sending service should use this to set the appropriate MIME type.
        /// </summary>
        bool IsHtml { get; }
    }
}