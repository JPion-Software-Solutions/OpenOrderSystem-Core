# Templates & Forms

`IEmailFormService` (implemented by `SmtpEmailService`) finds HTML templates and builds the forms that
`IEmailService.SendAsync(IEmailForm)` sends.

## Typical use

```csharp
var template = await forms.GetTemplateAsync("order-confirmation");
var form = forms.CreateHtmlEmailForm(template, HtmlEmailForm.GetDataFrom(model));
form.Sender = "no-reply@example.com";
form.SenderName = "Village Market";
form.Recipient = customer.Email;
form.Subject = "Your order is confirmed";
var result = await email.SendAsync(form);
```

`CreateHtmlEmailForm` doesn't fill in the addressing; set `Sender`, `SenderName`, `Recipient` and `Subject`
yourself.

## `IEmailFormService`

| Method | Behavior |
|---|---|
| `GetTemplateAsync(name)` | Looks for `<name>.html` in `email:overridedir`, then `email:builtindir`, and returns the first file found. Throws `EmailTemplateNotFoundException` if neither has it. Operators customize a template by putting a file with the same name in the override folder. |
| `CreateHtmlEmailForm(template, parameters)` | An `HtmlEmailForm` with the template and data set. |
| `CreatePlaintextEmailForm(sender, senderName, recipient, subject, body, cc?, bcc?)` | A ready-to-send plain-text form. |
| `ValidateTemplate(template)` | Checks a template for email-client problems (see below). |

## Forms

Both implement `IEmailForm`: `Sender`, `SenderName`, `Recipient`, `Cc`, `Bcc`, `Subject`, `Body`, `IsHtml`.
When `SenderName` is set, the From header is `Display Name <address>`; otherwise just the address. `Cc` and
`Bcc` each hold one address.

### `HtmlEmailForm`

| Member | Meaning |
|---|---|
| `HtmlTemplate` | The raw template |
| `TemplateData` | Token name → replacement value |
| `AddTemplateData(key, value)` / `AddTemplateData(dictionary)` | Add data. The dictionary overload lowercases keys; the single-key overload doesn't. Both throw on a duplicate key. |
| `RemoveTemplateData(key)` | Remove one entry |
| `Body` | The rendered HTML, built each time it's read |
| `OrphanedTokens` | Tokens in the template with no data, after `Body` was last read; `null` if all were filled |
| `GetDataFrom(object)` (static) | Turns an object's readable properties into template data, with lowercased names and `ToString()` values |

**Tokens** are written `{{name}}` and replaced with the matching `TemplateData` value. Unfilled tokens stay in
the output as written. Use lowercase token names with no spaces (see known issues).

### `PlaintextEmailForm`

All properties are set directly; `Body` is sent as-is.

## Template validation

`ValidateTemplate` (or `EmailTemplateValidator.Validate`) returns a `TemplateValidationReport` with `Findings`,
`HasFailures`, `HasWarnings` and `IsClean`.

| Severity | Code | Problem |
|---|---|---|
| Failure | `EMPTY_TEMPLATE` | Template is empty |
| Failure | `SCRIPT_TAG` | `<script>`: blocked everywhere, triggers spam filters |
| Failure | `EXTERNAL_STYLESHEET` | `<link rel="stylesheet">`: stripped; use inline styles |
| Failure | `FORM_TAG` | `<form>`: blocked by most clients |
| Failure | `IFRAME_TAG` | `<iframe>`: blocked everywhere |
| Failure | `UNCLOSED_TOKEN` | A `{{` without a matching `}}` |
| Warning | `STYLE_BLOCK` | `<style>`: stripped by Gmail and others |
| Warning | `CLASS_ATTRIBUTE` | `class=` does nothing once `<style>` is stripped |
| Warning | `ID_ATTRIBUTE` | `id=` is rarely useful in email |
| Warning | `UNSAFE_CSS` | CSS Outlook can't render (flex, grid, absolute positioning, float, animation, …) |
| Warning | `DIV_LAYOUT` | `<div>` layout renders inconsistently; use tables |
| Warning | `IMG_MISSING_ALT` / `IMG_MISSING_WIDTH` / `IMG_MISSING_HEIGHT` | `<img>` without that attribute |
| Warning | `UNKNOWN_TAG` | A tag outside the known-safe list |
| Warning | `MALFORMED_TOKEN` | A token name with special characters |

## Known issues

- **Tokens with capitals or spaces aren't replaced, and are reported as filled.** `BuildBody` lowercases token
  names and strips their spaces to match them against `TemplateData`, but then replaces the lowercase,
  space-free form. So `{{CustomerName}}` or `{{ name }}` matches its data, isn't replaced (the literal text
  differs), and is dropped from `OrphanedTokens`. The customer sees the raw token. Until fixed, write tokens in
  lowercase with no spaces.
- **`AddTemplateData(key, value)` doesn't lowercase `key`,** unlike the dictionary overload, so a capitalized key
  never matches.
