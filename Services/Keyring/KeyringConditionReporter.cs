using OpenOrderSystem.Core.Services.Keyring.Interfaces;
using OpenOrderSystem.Core.Services.UI.Notification.Interface;

namespace OpenOrderSystem.Core.Services.Keyring;

/// <summary>
/// Turns <see cref="KeyringStatus.Warnings"/> into notification conditions for the admin panel:
/// raises one for each warning that is present and resolves the rest. Call it after boot and
/// after any keyring change; it is safe to call repeatedly.
/// </summary>
/// <remarks>
/// Kept separate from the keyring so the keyring has no dependency on notifications (it is built
/// before the DI container exists).
/// </remarks>
public sealed class KeyringConditionReporter(IKeyringService keyring, INotificationService notifications)
{
    public const string PassThroughKey = "keyring.passthrough";
    public const string DataKeyUnwrappedKey = "keyring.data-key-unwrapped";
    public const string WeakEnvironmentKeyKey = "keyring.weak-environment-key";
    public const string StaleDataKeyVersionsKey = "keyring.stale-data-key-versions";

    private static readonly (KeyringWarnings Flag, string SourceKey, NotificationSeverity Severity, string Content)[] Conditions =
    [
        (KeyringWarnings.PassThrough, PassThroughKey, NotificationSeverity.Critical,
            "The keyring is in pass-through mode: secrets are stored unencrypted. Enable encryption in the keyring settings."),
        (KeyringWarnings.DataKeyUnwrapped, DataKeyUnwrappedKey, NotificationSeverity.Issue,
            "The keyring's data key is stored unencrypted next to the secrets. Set OOS_MASTERKEY and restart to protect it."),
        (KeyringWarnings.WeakEnvironmentKey, WeakEnvironmentKeyKey, NotificationSeverity.Issue,
            "OOS_MASTERKEY looks like a short passphrase. Replace it with a generated key (32+ random bytes, hex or base64)."),
        (KeyringWarnings.StaleDataKeyVersions, StaleDataKeyVersionsKey, NotificationSeverity.Issue,
            "Some keyring secrets use an older data key. Rotate the data key to re-encrypt them.")
    ];

    /// <summary>Raises or resolves each keyring condition to match the current status.</summary>
    /// <returns>The warnings that are currently raised.</returns>
    public async Task<KeyringWarnings> ReportAsync()
    {
        var warnings = keyring.GetStatus().Warnings;

        foreach (var (flag, sourceKey, severity, content) in Conditions)
        {
            if (warnings.HasFlag(flag))
                await notifications.RaiseConditionAsync(sourceKey, NotificationTarget.Admin, content, severity);
            else
                await notifications.ResolveConditionAsync(sourceKey);
        }

        return warnings;
    }
}
