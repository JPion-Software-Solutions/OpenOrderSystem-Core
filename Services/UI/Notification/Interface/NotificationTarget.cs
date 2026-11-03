using System.Text.RegularExpressions;

namespace OpenOrderSystem.Core.Services.UI.Notification.Interface;

/// <summary>Who a notification target addresses within its surface.</summary>
public enum NotificationRecipientKind
{
    /// <summary>Everyone on the surface (<c>system.admin</c>).</summary>
    Surface,

    /// <summary>One user (<c>system.admin:jdoe</c>).</summary>
    User,

    /// <summary>One group (<c>system.admin:g{managers}</c>).</summary>
    Group
}

/// <summary>
/// A parsed notification target: <c>surface[:recipient]</c>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><b>surface</b>: dot-separated segments, each a letter followed by letters or digits
///   (<c>system.admin</c>, <c>system.staffTerm</c>).</item>
///   <item><b>recipient</b> (optional): a user identifier, or <c>g{group}</c> for a group. Names use
///   <c>[A-Za-z0-9._@+-]</c>.</item>
/// </list>
/// Targets are case-insensitive and stored in lower case, so <c>system.staffTerm</c> is stored as
/// <c>system.staffterm</c>.
/// </remarks>
public readonly partial record struct NotificationTarget(
    string Surface, NotificationRecipientKind Kind, string? Recipient)
{
    /// <summary>General admin panel notifications.</summary>
    public const string Admin = "system.admin";

    /// <summary>General staff terminal notifications.</summary>
    public const string StaffTerminal = "system.staffTerm";

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SurfacePattern();

    [GeneratedRegex(@"^[a-z0-9._@+-]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    /// <summary>Parses and normalizes a target.</summary>
    /// <exception cref="ArgumentException">The target is not well formed.</exception>
    public static NotificationTarget Parse(string target, string paramName = "target")
        => TryParse(target, out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"'{target}' is not a valid notification target. Expected surface[:user] or surface:g{{group}}, " +
                "e.g. system.admin, system.admin:jdoe, system.admin:g{managers}.", paramName);

    public static bool TryParse(string? target, out NotificationTarget parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(target))
            return false;

        var colon = target.IndexOf(':');
        var surface = colon < 0 ? target : target[..colon];
        if (!SurfacePattern().IsMatch(surface))
            return false;
        surface = surface.ToLowerInvariant();

        if (colon < 0)
        {
            parsed = new NotificationTarget(surface, NotificationRecipientKind.Surface, null);
            return true;
        }

        var recipient = target[(colon + 1)..];
        if (recipient.StartsWith("g{", StringComparison.OrdinalIgnoreCase) && recipient.EndsWith('}'))
        {
            var group = recipient[2..^1];
            if (!NamePattern().IsMatch(group))
                return false;
            parsed = new NotificationTarget(surface, NotificationRecipientKind.Group, group.ToLowerInvariant());
            return true;
        }

        if (!NamePattern().IsMatch(recipient))
            return false;
        parsed = new NotificationTarget(surface, NotificationRecipientKind.User, recipient.ToLowerInvariant());
        return true;
    }

    /// <summary>Normalizes a target string; throws if it is not well formed.</summary>
    public static string Normalize(string target, string paramName = "target") => Parse(target, paramName).ToString();

    /// <summary>Builds <c>surface:user</c>.</summary>
    public static string ForUser(string surface, string user) => Parse($"{surface}:{user}", nameof(user)).ToString();

    /// <summary>Builds <c>surface:g{group}</c>.</summary>
    public static string ForGroup(string surface, string group) => Parse($"{surface}:g{{{group}}}", nameof(group)).ToString();

    public override string ToString() => Kind switch
    {
        NotificationRecipientKind.User => $"{Surface}:{Recipient}",
        NotificationRecipientKind.Group => $"{Surface}:g{{{Recipient}}}",
        _ => Surface
    };
}
