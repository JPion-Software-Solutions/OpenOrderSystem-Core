namespace OpenOrderSystem.Core.Services.EmailService.Exceptions;

/// <summary>
/// Thrown when SMTP settings are missing or invalid, before any connection is attempted.
/// </summary>
public class SmtpNotConfiguredException : InvalidOperationException
{
    public SmtpNotConfiguredException(string message) : base(message) { }
}
