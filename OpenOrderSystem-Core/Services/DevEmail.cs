using OpenOrderSystem.Core.Models.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;

namespace OpenOrderSystem.Core.Services
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
    }
}
