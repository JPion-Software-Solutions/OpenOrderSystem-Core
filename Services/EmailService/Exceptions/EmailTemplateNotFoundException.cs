namespace OpenOrderSystem.Core.Services.EmailService.Exceptions;

/// <summary>
/// Thrown when a requested email template cannot be found in any configured resolution layer.
/// </summary>
public class EmailTemplateNotFoundException : Exception
{
    public string TemplateName { get; }

    public EmailTemplateNotFoundException(string templateName)
        : base($"No email template found for key '{templateName}'.")
    {
        TemplateName = templateName;
    }

    public EmailTemplateNotFoundException(string templateName, string message)
        : base(message)
    {
        TemplateName = templateName;
    }
}