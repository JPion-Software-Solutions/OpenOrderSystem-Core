using OpenOrderSystem.Core.Services.EmailService.Interfaces;

namespace OpenOrderSystem.Core.Services.EmailService;

/// <summary>
/// Represents an HTML email form that supports token-based template rendering.
/// Tokens in the template are defined using double curly brace syntax, e.g. <c>{{tokenname}}</c>.
/// </summary>
public class HtmlEmailForm : IEmailForm
{
    /// <summary>
    /// Converts a plain object's readable properties into a <see cref="Dictionary{TKey,TValue}"/>
    /// suitable for use as template data. All keys are lowercased. Non-string property values
    /// are converted via <c>ToString()</c>.
    /// </summary>
    /// <param name="obj">The object to extract properties from.</param>
    /// <returns>A dictionary of lowercased property names mapped to their string values.</returns>
    public static Dictionary<string,string> GetDataFrom(object obj) =>
        obj.GetType().GetProperties()
            .Where(p => p.CanRead)
            .ToDictionary(
                p => p.Name.ToLowerInvariant(),
                p => p.GetValue(obj)?.ToString() ?? string.Empty
            );

    /// <summary>Gets or sets the sender's email address.</summary>
    public string Sender { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sender's display name. When set, the From header will be composed
    /// as <c>Display Name &lt;address@domain.com&gt;</c> rather than the bare address.
    /// This allows a shared sending address to present a different name per OOS instance,
    /// e.g. <c>Village Market &lt;no-reply@openordersystem.cloud&gt;</c>.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>Gets or sets the recipient's email address.</summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>Gets or sets the CC email address.</summary>
    public string Cc { get; set; } = string.Empty;

    /// <summary>Gets or sets the BCC email address.</summary>
    public string Bcc { get; set; } = string.Empty;

    /// <summary>Gets or sets the email subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets the rendered HTML body of the email, with all matched template tokens
    /// replaced by their corresponding values from <see cref="TemplateData"/>.
    /// Unresolved tokens are left as-is in the output and tracked in <see cref="OrphanedTokens"/>.
    /// </summary>
    public string Body => BuildBody();

    /// <summary>Gets or sets the raw HTML template string containing tokens to be replaced.</summary>
    public string HtmlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets the template data used for token substitution. Keys are lowercased.
    /// Use <see cref="AddTemplateData(string, string)"/> or <see cref="AddTemplateData(Dictionary{string, string})"/>
    /// to populate.
    /// </summary>
    public Dictionary<string,string> TemplateData { get; private set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets a dictionary of tokens that were present in the template but had no matching
    /// entry in <see cref="TemplateData"/> after rendering. Returns <c>null</c> if all
    /// tokens were resolved.
    /// </summary>
    public Dictionary<string, string>? OrphanedTokens { get; private set; } = null;

    /// <summary>Gets a value indicating that this form sends HTML email.</summary>
    public bool IsHtml => true;

    /// <summary>
    /// Adds a single key-value pair to the template data.
    /// </summary>
    /// <param name="key">The token name. Should match a <c>{{token}}</c> in the template.</param>
    /// <param name="value">The value to substitute for the token.</param>
    public void AddTemplateData(string key, string value) => TemplateData.Add(key, value);

    /// <summary>
    /// Adds multiple key-value pairs to the template data. All keys are lowercased on insertion.
    /// </summary>
    /// <param name="data">A dictionary of token names and their replacement values.</param>
    public void AddTemplateData(Dictionary<string, string> data)
    {
        foreach (var key in data.Keys)
        {
            TemplateData.Add(key.ToLowerInvariant(), data[key]);
        }
    }

    /// <summary>
    /// Removes a token entry from the template data by key.
    /// </summary>
    /// <param name="key">The token key to remove.</param>
    public void RemoveTemplateData(string key) => TemplateData.Remove(key);

    /// <summary>
    /// Scans <see cref="HtmlTemplate"/> for tokens, substitutes matched values from
    /// <see cref="TemplateData"/>, and populates <see cref="OrphanedTokens"/> with
    /// any tokens that could not be resolved.
    /// </summary>
    /// <returns>The rendered HTML string.</returns>
    private string BuildBody()
    {
        var tokens = new Dictionary<string, string>();
        var html = HtmlTemplate;

        for (int i = 0; i < html.Length; ++i)
        {
            i = html.IndexOf("{{", i, StringComparison.Ordinal); //jump to next token
            if (i < 0) break; //no more tokens found
            var j = html.IndexOf("}}", i, StringComparison.Ordinal); //find nearest close token
            if (j < 0) break; //no more tokens found

            var token = html.Substring(i + 2, j - i - 2);
            tokens[token.ToLowerInvariant().Replace(" ", "")] = string.Empty;
        }

        foreach (var token in TemplateData.Keys)
        {
            if (tokens.ContainsKey(token))
            {
                html = html.Replace("{{" + token + "}}", TemplateData[token]);
                tokens.Remove(token);
            }
        }

        OrphanedTokens = tokens.Keys.Any() ? tokens : null;
        return html;
    }
}