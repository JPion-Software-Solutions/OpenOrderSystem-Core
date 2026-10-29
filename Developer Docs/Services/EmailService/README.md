# Email Service

Outbound email: sending over SMTP, building messages from HTML templates, and reading the settings and
credentials that sending needs.

**Source:** `Services/EmailService/`

## Topics

| File | Covers |
|---|---|
| [IEmailService.md](IEmailService.md) | Sending, the connection check, SMTP security (TLS) and certificate trust, error codes |
| [Configuration.md](Configuration.md) | Every config key, `IEmailConfigService`, the keyring and insecure implementations, how the SMTP password is stored |
| [Templates-And-Forms.md](Templates-And-Forms.md) | `IEmailFormService`, HTML and plain-text forms, `{{token}}` templates, template lookup and validation |

## Architecture

| Type | Role |
|---|---|
| `IEmailService` | Sends mail and checks the connection. |
| `IEmailFormService` | Finds templates and builds `HtmlEmailForm` / `PlaintextEmailForm` objects to send. |
| `SmtpEmailService` | The real implementation of both, using MailKit. |
| `DevEmail` | Development `IEmailService` that prints to the console and always reports success. Doesn't implement `IEmailFormService`. |
| `IEmailConfigService` | Where `SmtpEmailService` reads its settings. Every key goes through it, so the sender never knows where a value is stored. |
| `KeyringEmailConfigService` | Production config: values in the config store, the password in the keyring. |
| `InsecureEmailConfigService` | Development config: everything, including the password, in a plain-text JSON file. |

## Registration

`RunOOS.cs` currently registers:

```csharp
services.AddScoped<IEmailService, SmtpEmailService>();
services.AddSingleton<IEmailConfigService, InsecureEmailConfigService>();
```

`KeyringEmailConfigService` can't be registered until the keyring is implemented (only its interfaces exist).
When it is, register it as **scoped**, because it depends on the scoped `IConfigurationStore`.

## Known issues

- **`HtmlEmailForm` misses tokens with capitals or spaces** and reports them as resolved. See
  [Templates-And-Forms.md](Templates-And-Forms.md#known-issues).
- **Any config value can reference any keyring secret.** `KeyringEmailConfigService` expands
  `<<key:name>>` tokens in every value, so someone who can edit email settings could put a secret's token in
  the sender name and email it to themselves. See [Configuration.md](Configuration.md#known-issues).
- **CC and BCC hold a single address each.**
