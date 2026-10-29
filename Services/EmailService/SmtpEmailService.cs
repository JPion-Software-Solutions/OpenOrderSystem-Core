using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

    public static class ConfigKeys
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
        var sender = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.DefaultSender) ?? string.Empty;
        var senderName = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.DefaultSenderName) ?? string.Empty;

        var form = isHtml
            ? (IEmailForm)CreateHtmlEmailForm(body, new Dictionary<string, string>())
            : CreatePlaintextEmailForm(sender, senderName, recipient, subject, body);

        // For the raw string overload, manually set addressing if using HtmlEmailForm
        if (form is HtmlEmailForm htmlForm)
        {
            htmlForm.Sender = sender;
            htmlForm.SenderName = senderName;
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
            await ConnectAndAuthenticateAsync(client);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'.",
                emailForm.Recipient, emailForm.Subject);

            return new EmailSendResult { Success = true };
        }
        catch (SmtpNotConfiguredException ex)
        {
            _logger.LogError("Email to {Recipient} not sent: {Message}", emailForm.Recipient, ex.Message);
            return new EmailSendResult
            {
                Success = false,
                ErrorCode = "NOT_CONFIGURED",
                ErrorMessage = ex.Message
            };
        }
        catch (SslHandshakeException ex)
        {
            _logger.LogError(ex, "TLS handshake with the SMTP server failed sending to {Recipient}.", emailForm.Recipient);
            return new EmailSendResult
            {
                Success = false,
                ErrorCode = "TLS_ERROR",
                ErrorMessage = "The secure connection to the mail server failed. Check the security mode and port, " +
                               "and whether the server's certificate is trusted."
            };
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

    public async Task<bool> CheckConnectionAsync()
    {
        try
        {
            using var client = new SmtpClient();
            await ConnectAndAuthenticateAsync(client);
            await client.DisconnectAsync(true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP connection check failed.");
            return false;
        }
    }

    /// <summary>
    /// Connects using the configured host, port, security mode and certificate trust, then authenticates if a
    /// username is configured. Shared by sending and the connection check so both behave identically.
    /// </summary>
    /// <exception cref="SmtpNotConfiguredException">The host is missing, or the port or security mode is invalid.</exception>
    private async Task ConnectAndAuthenticateAsync(SmtpClient client)
    {
        var host = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.Host);
        if (string.IsNullOrWhiteSpace(host))
            throw new SmtpNotConfiguredException("The SMTP host isn't configured.");

        var portValue = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.Port) ?? "587";
        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
            throw new SmtpNotConfiguredException($"The SMTP port '{portValue}' isn't a valid port number.");

        var socketOptions = await GetSecureSocketOptionsAsync(port);
        client.ServerCertificateValidationCallback = await BuildCertificateValidatorAsync();

        await client.ConnectAsync(host, port, socketOptions);

        var username = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.Username);
        if (!string.IsNullOrWhiteSpace(username))
            await client.AuthenticateAsync(username, await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.Password) ?? string.Empty);
    }

    /// <summary>
    /// The configured <see cref="SecureSocketOptions"/> (any member name, case-insensitive). When unset: implicit TLS
    /// on port 465, required STARTTLS on every other port. <c>Auto</c> isn't the default because on most ports it
    /// silently falls back to plain text when the server doesn't offer STARTTLS.
    /// </summary>
    private async Task<SecureSocketOptions> GetSecureSocketOptionsAsync(int port)
    {
        var configured = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.SecureSocketOption);
        if (string.IsNullOrWhiteSpace(configured))
            return port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        if (!Enum.TryParse<SecureSocketOptions>(configured, ignoreCase: true, out var options) || !Enum.IsDefined(options))
            throw new SmtpNotConfiguredException(
                $"The SMTP security mode '{configured}' isn't recognized. Use one of: {string.Join(", ", Enum.GetNames<SecureSocketOptions>())}.");

        if (options == SecureSocketOptions.None)
            _logger.LogWarning("SMTP is configured without TLS; credentials and mail are sent in plain text.");
        return options;
    }

    /// <summary>
    /// Accepts a certificate the system already trusts. Otherwise accepts it only when <c>smtp:acceptallcerts</c> is
    /// <c>true</c>, or its SHA-1 or SHA-256 thumbprint matches <c>smtp:acceptcertthumbprint</c> (separators and case ignored),
    /// for servers with self-signed or privately issued certificates.
    /// </summary>
    private async Task<RemoteCertificateValidationCallback> BuildCertificateValidatorAsync()
    {
        var acceptAll = bool.TryParse(await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.AcceptAllCerts), out var all) && all;
        var pinned = NormalizeThumbprint(await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.AcceptCertThumbprint));
        var logger = _logger;

        return (_, certificate, _, errors) =>
        {
            if (errors == SslPolicyErrors.None)
                return true;

            if (acceptAll)
            {
                logger.LogWarning("Accepting an untrusted SMTP server certificate ({Errors}) because smtp:acceptallcerts is enabled.", errors);
                return true;
            }

            if (pinned is not null && certificate is not null)
            {
                var cert = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
                if (pinned == cert.GetCertHashString(HashAlgorithmName.SHA1) || pinned == cert.GetCertHashString(HashAlgorithmName.SHA256))
                    return true;
            }

            logger.LogError("Rejected the SMTP server certificate ({Errors}). Trust it with smtp:acceptcertthumbprint if it is expected.", errors);
            return false;
        };
    }

    private static string? NormalizeThumbprint(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();

    public async Task<string> GetTemplateAsync(string templateName)
    {
        var overrideDir = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.OverrideDirectory);
        var builtInDir = await _config.GetKeyAsync<SmtpEmailService>(ConfigKeys.BuiltInDirectory);
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