# Email Configuration

`SmtpEmailService` reads every setting through `IEmailConfigService`. Keys are constants on
`SmtpEmailService.ConfigKeys`.

## Keys

| Key | Meaning | Default |
|---|---|---|
| `smtp:host` | SMTP server host name | **Required** |
| `smtp:port` | SMTP port | `587` |
| `smtp:securesocket` | TLS mode (see [IEmailService.md](IEmailService.md#how-smtpemailservice-connects)) | Based on port |
| `smtp:username` | Login name. Leave unset for an unauthenticated relay | — |
| `smtp:password` | Login password. **Keyring only** in production (see below) | — |
| `smtp:acceptallcerts` | `true` to accept any server certificate. Testing only | `false` |
| `smtp:acceptcertthumbprint` | SHA-1 or SHA-256 thumbprint of one trusted server certificate | — |
| `smtp:sender` | Default From address for quick sends | — |
| `smtp:sendername` | Default From display name, e.g. `Village Market` | — |
| `email:overridedir` | Folder of operator-customized templates, checked first | — |
| `email:builtindir` | Folder of built-in templates | — |

All values are strings: `"587"`, `"true"`.

## `IEmailConfigService`

| Method | Behavior |
|---|---|
| `GetKeyAsync<T>(key)` | The value, or `null` if unset. `T` is the calling service (e.g. `SmtpEmailService`) and is reserved for scoping keys per service; neither implementation uses it yet. |
| `SetKeyAsync<T>(key, value)` | Stores a value. Can throw `SecretConfigKeyException` (keyring-backed keys). |

## `KeyringEmailConfigService` (production)

Scoped. Values live in the config store (`IConfigurationStore`). Secrets live in the keyring and are referenced
from config values with tokens.

- **Reading:** the stored value is passed through `IReadOnlyKeyring.ExpandSecrets`, which replaces every
  `<<key:name>>` token with that keyring secret's value. Values without tokens come back unchanged. Blank
  values are returned as `null`.
- **A missing secret:** if a value references a secret that isn't in the keyring, `GetKeyAsync` returns `null`
  and logs a warning naming the secret (never a value).
- **Locked keys:** `SetKeyAsync` on a key locked in the config store throws `InvalidOperationException`.

### The SMTP password

The password is **never** stored as plain text in production and can't be written through this service.

1. A privileged admin adds a keyring secret named **`smtp:password`**.
2. When `smtp:username` is saved through `SetKeyAsync`, the service also stores the token
   `<<key:smtp:password>>` as the `smtp:password` config value, pointing it at that secret.
3. `GetKeyAsync("smtp:password")` expands the token and returns the secret.

`SetKeyAsync("smtp:password", …)` always throws **`SecretConfigKeyException`**, which carries `Key` and
`SecretName`. The UI should catch it and send the admin to the keyring entry instead of showing a failure.

The token is only written when the username is saved through this service. A config store populated some other
way (seeded or migrated) also needs the `smtp:password` token written, or the password resolves to `null`.

To store the password as plain text (development, or hobbyist installs that accept the risk), use
`InsecureEmailConfigService` instead.

## `InsecureEmailConfigService` (development)

Stores every key, password included, in plain text at
`<DataRoot>/config/DANGER_NOT_FOR_PRODUCTION/dangerEmailConfig.json`. The file is created empty on first use.
Accepts any key, never throws for secrets, and doesn't expand tokens.

> ⚠️ Not for production. Keep that folder out of source control and backups that leave the machine.

## Known issues

- **Any value can reference any keyring secret.** Tokens are expanded in every key, and `SetKeyAsync` accepts
  token text in any key except the password. Someone who can edit email settings but shouldn't see keyring
  values could set `smtp:sendername` to `<<key:smtp:password>>` (or any other secret's name) and email
  themselves; the secret arrives in the From header. Fix by expanding tokens only for an allowlist of keys, or
  rejecting token text in `SetKeyAsync` for other keys.
- **Unknown keys are accepted** by both implementations.
- **`KeyringEmailConfigService`'s XML docs are out of date:** they describe a key allowlist, a default password
  token and an unknown-key exception that were removed.
- **`InsecureEmailConfigService` re-reads the file on every call** and has no locking, so two simultaneous
  writes can lose one. It's development-only.
