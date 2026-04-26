using System.Text.RegularExpressions;

namespace OpenOrderSystem.Core.Services.EmailService;

/// <summary>
/// Represents the severity of a template validation finding.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>A critical issue that will likely break delivery or trigger spam filters.</summary>
    Failure,
    /// <summary>A non-critical issue that may cause rendering problems in some email clients.</summary>
    Warning
}

/// <summary>
/// Represents a single finding produced by <see cref="EmailTemplateValidator.Validate"/>.
/// </summary>
public record ValidationFinding(ValidationSeverity Severity, string Code, string Message);

/// <summary>
/// Represents the output of an email template validation pass.
/// </summary>
public class TemplateValidationReport
{
    /// <summary>Gets the list of findings produced during validation.</summary>
    public List<ValidationFinding> Findings { get; } = new();

    /// <summary>Gets a value indicating whether any hard failures were found.</summary>
    public bool HasFailures => Findings.Any(f => f.Severity == ValidationSeverity.Failure);

    /// <summary>Gets a value indicating whether any warnings were found.</summary>
    public bool HasWarnings => Findings.Any(f => f.Severity == ValidationSeverity.Warning);

    /// <summary>Gets a value indicating whether the template passed validation with no findings.</summary>
    public bool IsClean => !Findings.Any();

    /// <summary>Gets only the hard failure findings.</summary>
    public IEnumerable<ValidationFinding> Failures => 
        Findings.Where(f => f.Severity == ValidationSeverity.Failure);

    /// <summary>Gets only the warning findings.</summary>
    public IEnumerable<ValidationFinding> Warnings => 
        Findings.Where(f => f.Severity == ValidationSeverity.Warning);
}

/// <summary>
/// Provides static validation of HTML email templates against known email client
/// compatibility rules. Produces a <see cref="TemplateValidationReport"/> containing
/// hard failures and warnings.
/// </summary>
public static class EmailTemplateValidator
{
    // Tags that are known to be safe across major email clients
    private static readonly HashSet<string> SafeTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "table", "thead", "tbody", "tr", "td", "th",
        "p", "span", "a", "img", "br", "hr",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "strong", "em", "b", "i", "u",
        "ul", "ol", "li",
        "html", "head", "body", "meta", "title"
    };

    // CSS properties unsupported by Outlook's Word-based renderer
    private static readonly HashSet<string> UnsafeCssProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "display:flex", "display:grid", "display:inline-flex", "display:inline-grid",
        "position:absolute", "position:fixed", "position:sticky",
        "float", "flexbox", "grid-template", "grid-column", "grid-row",
        "animation", "transition", "transform"
    };

    /// <summary>
    /// Validates an HTML email template string against known email client compatibility rules.
    /// </summary>
    /// <param name="template">The raw HTML template string to validate.</param>
    /// <returns>
    /// A <see cref="TemplateValidationReport"/> containing any failures and warnings found.
    /// Check <see cref="TemplateValidationReport.IsClean"/> for a pass/fail summary.
    /// </returns>
    public static TemplateValidationReport Validate(string template)
    {
        var report = new TemplateValidationReport();

        if (string.IsNullOrWhiteSpace(template))
        {
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Failure, "EMPTY_TEMPLATE", "Template is null or empty."));
            return report;
        }

        CheckHardFailures(template, report);
        CheckWarnings(template, report);
        CheckTokenSyntax(template, report);

        return report;
    }

    private static void CheckHardFailures(string template, TemplateValidationReport report)
    {
        // Script tags — will trigger spam filters and are stripped by all major clients
        if (Regex.IsMatch(template, @"<script[\s>]", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Failure, "SCRIPT_TAG",
                "<script> tags are blocked by all major email clients and will trigger spam filters."));

        // External stylesheets
        if (Regex.IsMatch(template, @"<link[^>]+rel=[""']stylesheet[""']", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Failure, "EXTERNAL_STYLESHEET",
                "<link> stylesheet references are stripped by most email clients. Use inline styles."));

        // Form elements
        if (Regex.IsMatch(template, @"<form[\s>]", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Failure, "FORM_TAG",
                "<form> elements are blocked by most email clients."));

        // iframe
        if (Regex.IsMatch(template, @"<iframe[\s>]", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Failure, "IFRAME_TAG",
                "<iframe> elements are blocked by all major email clients."));
    }

    private static void CheckWarnings(string template, TemplateValidationReport report)
    {
        // Inline <style> blocks — stripped by Gmail and others
        if (Regex.IsMatch(template, @"<style[\s>]", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Warning, "STYLE_BLOCK",
                "<style> blocks are stripped by Gmail and several other clients. Use inline styles."));

        // class attributes — ignored when <style> is stripped
        if (Regex.IsMatch(template, @"\bclass\s*=", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Warning, "CLASS_ATTRIBUTE",
                "class attributes have no effect without a <style> block. Use inline style attributes."));

        // id attributes
        if (Regex.IsMatch(template, @"\bid\s*=", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Warning, "ID_ATTRIBUTE",
                "id attributes are unlikely to be useful in email templates and may cause issues in some clients."));

        // Unsafe CSS properties in inline styles
        foreach (var property in UnsafeCssProperties)
        {
            if (template.Contains(property, StringComparison.OrdinalIgnoreCase))
                report.Findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning, "UNSAFE_CSS",
                    $"CSS property '{property}' is not supported by Outlook's rendering engine."));
        }

        // div as layout element
        if (Regex.IsMatch(template, @"<div[\s>]", RegexOptions.IgnoreCase))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Warning, "DIV_LAYOUT",
                "<div> elements may render inconsistently. Use <table> for layout."));

        // img tags missing alt, width, or height
        foreach (Match img in Regex.Matches(template, @"<img[^>]*>", RegexOptions.IgnoreCase))
        {
            var tag = img.Value;
            if (!Regex.IsMatch(tag, @"\balt\s*=", RegexOptions.IgnoreCase))
                report.Findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning, "IMG_MISSING_ALT",
                    $"An <img> tag is missing an alt attribute: {tag}"));
            if (!Regex.IsMatch(tag, @"\bwidth\s*=", RegexOptions.IgnoreCase))
                report.Findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning, "IMG_MISSING_WIDTH",
                    $"An <img> tag is missing a width attribute: {tag}"));
            if (!Regex.IsMatch(tag, @"\bheight\s*=", RegexOptions.IgnoreCase))
                report.Findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning, "IMG_MISSING_HEIGHT",
                    $"An <img> tag is missing a height attribute: {tag}"));
        }

        // Unknown tags
        foreach (Match tag in Regex.Matches(template, @"<([a-zA-Z][a-zA-Z0-9]*)", RegexOptions.IgnoreCase))
        {
            var tagName = tag.Groups[1].Value;
            if (!SafeTags.Contains(tagName))
                report.Findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning, "UNKNOWN_TAG",
                    $"<{tagName}> is not in the known-safe tag list and may be stripped by some clients."));
        }
    }

    private static void CheckTokenSyntax(string template, TemplateValidationReport report)
    {
        // Unclosed opening braces
        foreach (Match match in Regex.Matches(template, @"\{\{([^}]*)$", RegexOptions.Multiline))
            report.Findings.Add(new ValidationFinding(
                ValidationSeverity.Failure, "UNCLOSED_TOKEN",
                $"Unclosed token starting with '{{{{': {match.Value.Trim()}"));

        // Malformed tokens with spaces or special characters
        foreach (Match match in Regex.Matches(template, @"\{\{([^}]+)\}\}", RegexOptions.IgnoreCase))
        {
            var token = match.Groups[1].Value;
            if (Regex.IsMatch(token, @"[^a-zA-Z0-9_\s]"))
                report.Findings.Add(new ValidationFinding(
                    ValidationSeverity.Warning, "MALFORMED_TOKEN",
                    $"Token '{{{{ {token} }}}}' contains special characters that may cause substitution issues."));
        }
    }
}