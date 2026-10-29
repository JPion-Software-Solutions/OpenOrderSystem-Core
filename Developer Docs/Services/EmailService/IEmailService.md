# IEmailService

`Services/EmailService/Interfaces/IEmailService.cs`, implemented by `SmtpEmailService` (scoped) and `DevEmail`.

## Methods

| Method | Behavior |
|---|---|
| `SendAsync(recipient, subject, body, isHtml = false)` | Quick send. The From address and display name come from `smtp:sender` and `smtp:sendername`. With `isHtml = true`, `body` is treated as an HTML template with no data (any `{{tokens}}` are left as they are). |
| `SendAsync(IEmailForm form)` | Sends a prepared form (see [Templates-And-Forms.md](Templates-And-Forms.md)). The form's own `Sender` and `SenderName` are used. |
| `CheckConnectionAsync()` | Connects and authenticates exactly as sending does, then disconnects. `true` on success; failures are logged as warnings and return `false`. |

Neither method throws for mail failures. `SendAsync` returns an `EmailSendResult`.

## `EmailSendResult`

| Field | Meaning |
|---|---|
| `Success` | Whether the message was accepted by the server |
| `ErrorCode` | See below. Null on success (`DevEmail` returns empty strings) |
| `ErrorMessage` | Detail; safe to show an admin |

| `ErrorCode` | When |
|---|---|
| `NOT_CONFIGURED` | Host missing, or the port or security mode is invalid. Detected before connecting |
| `TLS_ERROR` | The secure connection failed: wrong security mode for the port, or an untrusted certificate |
| SMTP status code (e.g. `MailboxUnavailable`) | The server rejected a command (bad credentials, refused recipient, …) |
| `PROTOCOL_ERROR` | The server broke the SMTP protocol |
| `UNEXPECTED_ERROR` | Anything else (network failure, invalid address format, …) |

## How `SmtpEmailService` connects

Sending and the connection check share one routine, so a passing check means sending will connect the same way.

1. **Host** (`smtp:host`) is required.
2. **Port** (`smtp:port`) defaults to `587`, and must be 1–65535.
3. **Security mode** (`smtp:securesocket`): any MailKit `SecureSocketOptions` name, case-insensitive.

   | Value | Meaning |
   |---|---|
   | *(unset)* | Port 465: `SslOnConnect`. Any other port: `StartTls` |
   | `SslOnConnect` | TLS from the first byte (usually port 465) |
   | `StartTls` | Plain connection upgraded to TLS; fails if the server doesn't offer it (usually 587) |
   | `StartTlsWhenAvailable` | Upgrades if offered, otherwise stays plain text |
   | `Auto` | MailKit picks based on the port; can fall back to plain text |
   | `None` | No TLS. Logs a warning on every connection |

   `Auto` isn't the default because on most ports it silently drops to plain text when the server doesn't offer TLS.
4. **Certificate trust:** a certificate the operating system trusts is always accepted. An untrusted one
   (self-signed, private CA, expired, wrong name) is accepted only if:
   - `smtp:acceptallcerts` is `true`, which logs a warning on every connection. Use for testing only.
   - Or its thumbprint matches `smtp:acceptcertthumbprint`. SHA-1 or SHA-256 hex; colons, spaces and case are
     ignored. This is the right way to trust one specific self-signed server.

   A rejected certificate is logged as an error that names the thumbprint setting.
5. **Authentication** runs only when `smtp:username` is set, using `smtp:password` (see
   [Configuration.md](Configuration.md#the-smtp-password)). With no username, the server is used as an
   unauthenticated relay.

A new client is opened for every message.
