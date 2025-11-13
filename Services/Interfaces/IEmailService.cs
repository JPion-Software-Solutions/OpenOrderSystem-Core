using OpenOrderSystem.Core.Models.Interfaces;

namespace OpenOrderSystem.Core.Services.Interfaces
{
    public interface IEmailService
    {
        /// <summary>
        /// Send a basic email directly without using a preformatted form
        /// </summary>
        /// <param name="recipient">Recipient</param>
        /// <param name="subject">Subject line</param>
        /// <param name="body">Message body</param>
        /// <param name="isHtml">False (default) if the message is plaintext</param>
        public void Send(string recipient, string subject, string body, bool isHtml = false);

        /// <summary>
        /// Send an email with a preformatted email form.
        /// </summary>
        /// <param name="emailForm">Any preformatted email form</param>
        public void Send(IEmailForm emailForm);
    }
}
