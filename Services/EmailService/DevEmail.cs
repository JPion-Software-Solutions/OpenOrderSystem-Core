using OpenOrderSystem.Core.Services.EmailService.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService
{
    public class DevEmail : IEmailService
    {
        public void Send(string recipient, string subject, string body, bool isHtml = false)
        {
            Console.WriteLine("**************** EMAIL SENT ****************");
            Console.WriteLine($"\tRECIPIENT: {recipient}");
            Console.WriteLine($"\t  SUBJECT: {subject}");
            Console.WriteLine($"\t     BODY: {body}");
        }

        public void Send(IEmailForm emailForm)
        {
            Console.WriteLine("Email form sent.");
        }

        public async Task<EmailSendResult> SendAsync(string recipient, string subject, string body, bool isHtml = false)
        {
            Send(recipient, subject, body, isHtml);
            return new EmailSendResult
            {
                Success =  true,
                ErrorCode = string.Empty,
                ErrorMessage = string.Empty
            };
        }

        public async Task<EmailSendResult> SendAsync(IEmailForm emailForm)
        {
            Send(emailForm);
            return new EmailSendResult
            {
                Success =  true,
                ErrorCode = string.Empty,
                ErrorMessage = string.Empty
            };
        }

        public bool CheckConnection() => true;
    }
}
