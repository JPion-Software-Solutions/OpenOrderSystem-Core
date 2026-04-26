namespace OpenOrderSystem.Core.Services.EmailService.Interfaces
{
    public interface IEmailService
    {
        public Task<EmailSendResult> SendAsync(string recipient, string subject, string body, bool isHtml = false);

        public Task<EmailSendResult> SendAsync(IEmailForm emailForm);
        
        public bool CheckConnection();
    }
}
