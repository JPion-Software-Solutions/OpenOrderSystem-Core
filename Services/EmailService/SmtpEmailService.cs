using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Services.EmailService.Exceptions;
using OpenOrderSystem.Core.Services.EmailService.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService;

public class SmtpEmailService : IEmailService, IEmailFormService
{
    private readonly IEmailConfigService _config;
    private readonly ILogger<SmtpEmailService> _logger;

    private static class ConfigKeys
    {
        public const string Host = "smtp:host";
        public const string Port = "smtp:port";
        public const string Username = "smtp:username";
        public const string Password = "smtp:password";
        public const string DefaultSender = "smtp:sender";
        public const string DefaultSenderName = "smtp:sendername";
        public const string OverrideDirectory = "email:overridedir";
        public const string BuiltInDirectory = "email:builtindir";
        public const string SecureSocketOption = "smtp:securesocket";
        public const string AcceptAllCerts = "smtp:acceptallcerts";
        public const string AcceptCertThumbprint = "smtp:acceptcertthumbprint";
    }

    public SmtpEmailService(IEmailConfigService config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(string recipient, string subject, string body, bool isHtml = false)
    {
        var form = isHtml
            ? (IEmailForm)CreateHtmlEmailForm(body, new Dictionary<string, string>())
            : CreatePlaintextEmailForm(
                _config.GetKey<SmtpEmailService>(ConfigKeys.DefaultSender) ?? string.Empty,
                _config.GetKey<SmtpEmailService>(ConfigKeys.DefaultSenderName) ?? string.Empty,
                recipient, subject, body);

        // For the raw string overload, manually set addressing if using HtmlEmailForm
        if (form is HtmlEmailForm htmlForm)
        {
            htmlForm.Sender = _config.GetKey<SmtpEmailService>(ConfigKeys.DefaultSender) ?? string.Empty;
            htmlForm.SenderName = _config.GetKey<SmtpEmailService>(ConfigKeys.DefaultSenderName) ?? string.Empty;
            htmlForm.Recipient = recipient;
            htmlForm.Subject = subject;
        }

        return await SendAsync(form);
    }

    public async Task<EmailSendResult> SendAsync(IEmailForm emailForm)
    {
        try
        {
            var message = BuildMimeMessage(emailForm);

            using var client = new SmtpClient();
            
            var host = _config.GetKey<SmtpEmailService>(ConfigKeys.Host);
            var port = _config.GetKey<SmtpEmailService>(ConfigKeys.Port) ?? "587";
            var username =  _config.GetKey<SmtpEmailService>(ConfigKeys.Username);
            var password = _config.GetKey<SmtpEmailService>(ConfigKeys.Password);

            await client.ConnectAsync(
                host,
                int.Parse(port),
                SecureSocketOptions.SslOnConnect);

            await client.AuthenticateAsync(username, password);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'.",
                emailForm.Recipient, emailForm.Subject);

            return new EmailSendResult { Success = true };
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogError(ex, "SMTP command error sending to {Recipient}: [{Code}] {Message}",
                emailForm.Recipient, ex.StatusCode, ex.Message);
            return new EmailSendResult
            {
                Success = false,
                ErrorCode = ex.StatusCode.ToString(),
                ErrorMessage = ex.Message
            };
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "SMTP protocol error sending to {Recipient}.", emailForm.Recipient);
            return new EmailSendResult
            {
                Success = false,
                ErrorCode = "PROTOCOL_ERROR",
                ErrorMessage = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email to {Recipient}.", emailForm.Recipient);
            return new EmailSendResult
            {
                Success = false,
                ErrorCode = "UNEXPECTED_ERROR",
                ErrorMessage = ex.Message
            };
        }
    }

    public bool CheckConnection()
    {
        try
        {
            using var client = new SmtpClient();
            client.Connect(
                _config.GetKey<SmtpEmailService>(ConfigKeys.Host),
                int.Parse(_config.GetKey<SmtpEmailService>(ConfigKeys.Port) ?? "587"),
                SecureSocketOptions.StartTls);
            client.Authenticate(
                _config.GetKey<SmtpEmailService>(ConfigKeys.Username),
                _config.GetKey<SmtpEmailService>(ConfigKeys.Password));
            client.Disconnect(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP connection check failed.");
            return false;
        }
    }

    public async Task<string> GetTemplateAsync(string templateName)
    {
        var overrideDir = _config.GetKey<SmtpEmailService>(ConfigKeys.OverrideDirectory);
        var builtInDir = _config.GetKey<SmtpEmailService>(ConfigKeys.BuiltInDirectory);
        var fileName = $"{templateName}.html";

        if (overrideDir is not null)
        {
            var overridePath = Path.Combine(overrideDir, fileName);
            if (File.Exists(overridePath))
            {
                _logger.LogInformation("Resolved template '{TemplateName}' from override directory.", templateName);
                return await File.ReadAllTextAsync(overridePath);
            }
        }

        if (builtInDir is not null)
        {
            var builtInPath = Path.Combine(builtInDir, fileName);
            if (File.Exists(builtInPath))
            {
                _logger.LogInformation("Resolved template '{TemplateName}' from built-in directory.", templateName);
                return await File.ReadAllTextAsync(builtInPath);
            }
        }

        _logger.LogError("Email template '{TemplateName}' could not be found in any resolution layer.", templateName);
        throw new EmailTemplateNotFoundException(templateName);
    }

    public HtmlEmailForm CreateHtmlEmailForm(string template, Dictionary<string, string> parameters)
    {
        var form = new HtmlEmailForm { HtmlTemplate = template };
        form.AddTemplateData(parameters);
        return form;
    }

    public PlaintextEmailForm CreatePlaintextEmailForm(string sender, string senderName, string recipient,
        string subject, string body, string? cc = null, string? bcc = null)
    {
        return new PlaintextEmailForm
        {
            Sender = sender,
            SenderName = senderName,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Cc = cc ?? string.Empty,
            Bcc = bcc ?? string.Empty
        };
    }

    private static MimeMessage BuildMimeMessage(IEmailForm form)
    {
        var message = new MimeMessage();

        message.From.Add(string.IsNullOrWhiteSpace(form.SenderName)
            ? new MailboxAddress(form.Sender, form.Sender)
            : new MailboxAddress(form.SenderName, form.Sender));

        message.To.Add(MailboxAddress.Parse(form.Recipient));

        if (!string.IsNullOrWhiteSpace(form.Cc))
            message.Cc.Add(MailboxAddress.Parse(form.Cc));

        if (!string.IsNullOrWhiteSpace(form.Bcc))
            message.Bcc.Add(MailboxAddress.Parse(form.Bcc));

        message.Subject = form.Subject;

        message.Body = form.IsHtml
            ? new TextPart("html") { Text = form.Body }
            : new TextPart("plain") { Text = form.Body };

        return message;
    }
}