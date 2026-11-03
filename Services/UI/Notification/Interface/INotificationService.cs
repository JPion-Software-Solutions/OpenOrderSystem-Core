using System.Text.Json.Serialization;

namespace OpenOrderSystem.Core.Services.UI.Notification.Interface;

/// <summary>
/// Posts notifications to a target and tracks whether they have been alerted and answered.
/// Shared by every OOS surface (admin panel, staff terminal, ...); each surface decides how
/// to present them, but sending and receiving work the same everywhere.
/// </summary>
/// <remarks>
/// <para>
/// Targets use the form <c>surface[:recipient]</c>. See <see cref="NotificationTarget"/> for the
/// grammar. Examples: <c>system.admin</c> (every admin), <c>system.staffTerm</c> (every staff
/// terminal), <c>system.admin:jdoe</c> (one user), <c>system.admin:g{managers}</c> (one group).
/// Targets are compared case-insensitively.
/// </para>
/// <para>
/// There are two kinds of notification:
/// <list type="bullet">
///   <item><b>Events</b> (<see cref="PostNotificationAsync"/>): something happened. Each post
///   creates a new notification.</item>
///   <item><b>Conditions</b> (<see cref="RaiseConditionAsync"/>): something is true until it is
///   fixed, such as the keyring running unencrypted. Raising the same condition again updates
///   the existing notification instead of duplicating it. The owner clears it with
///   <see cref="ResolveConditionAsync"/>. A user can also dismiss it with
///   <see cref="AnswerNotificationAsync"/>; if the condition still holds, the owner's next
///   <see cref="RaiseConditionAsync"/> (for example on the next boot) opens a new one.</item>
/// </list>
/// </para>
/// <para>
/// Any notification can be answered, whatever its severity. Guarding against an accidental
/// dismissal (such as asking for confirmation on a critical one) is the caller's job, not the
/// service's.
/// </para>
/// <para>
/// Every write is durable when its task completes: the change has been flushed to disk.
/// If persisting fails, the task faults and in-memory state is unchanged, so a notification
/// is never reported as posted and then lost.
/// </para>
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// A stored notification.
    /// </summary>
    /// <param name="Id">Assigned by the service.</param>
    /// <param name="Target">Normalized target the notification was posted to.</param>
    /// <param name="Content">Display text.</param>
    /// <param name="Severity">How intrusively to present it.</param>
    /// <param name="CreatedAt">When it was first posted or raised.</param>
    /// <param name="AnsweredAt">When it was acknowledged, dismissed, or (for conditions) resolved; null while open.</param>
    /// <param name="AlertedAt">When the toast/sound was presented; null if the alert has not been shown yet.</param>
    /// <param name="SourceKey">The condition key for notifications from <see cref="RaiseConditionAsync"/>; null for events.</param>
    record Notification(
        Guid Id,
        string Target,
        string Content,
        NotificationSeverity Severity,
        DateTimeOffset CreatedAt,
        DateTimeOffset? AnsweredAt,
        DateTimeOffset? AlertedAt,
        string? SourceKey = null)
    {
        [JsonIgnore] public bool IsOpen => AnsweredAt is null;
        [JsonIgnore] public bool IsCondition => SourceKey is not null;
    }

    /// <summary>
    /// Returns notifications posted to exactly <paramref name="target"/>, open and answered,
    /// newest first. Use <see cref="GetNotificationsForRecipientAsync"/> to collect everything a
    /// signed-in user should see.
    /// </summary>
    /// <param name="target">
    /// The exact target to match, e.g. <c>system.admin</c> or <c>system.admin:jdoe</c>. Case-insensitive.
    /// </param>
    /// <exception cref="ArgumentException">The target is not well formed.</exception>
    Task<ICollection<Notification>> GetNotificationsAsync(string target);

    /// <summary>
    /// Returns every notification a recipient on <paramref name="surface"/> should see: those
    /// posted to the surface itself, to <c>surface:user</c>, and to <c>surface:g{group}</c> for
    /// each group. Newest first.
    /// </summary>
    /// <param name="surface">A surface such as <c>system.admin</c>, with no recipient part.</param>
    /// <param name="user">The user's identifier, or null for surface-wide notifications only.</param>
    /// <param name="groups">Groups the user belongs to.</param>
    /// <exception cref="ArgumentException">The surface, user, or a group name is not well formed.</exception>
    Task<ICollection<Notification>> GetNotificationsForRecipientAsync(
        string surface, string? user = null, IEnumerable<string>? groups = null);

    /// <summary>
    /// Returns the notification with this id, or null if it does not exist (or was pruned).
    /// </summary>
    /// <param name="notificationId">The id returned in <see cref="NotificationResult.Id"/> when it was posted or raised.</param>
    Task<Notification?> GetNotificationAsync(Guid notificationId);

    /// <summary>
    /// Marks a notification as acknowledged or dismissed. Works for every severity and for
    /// conditions; any confirmation step belongs to the caller.
    /// </summary>
    /// <param name="notificationId">The notification to answer.</param>
    /// <returns>
    /// <see cref="NotificationResultState.Updated"/>; <see cref="NotificationResultState.Unchanged"/>
    /// if already answered; or <see cref="NotificationResultState.NotFound"/>.
    /// </returns>
    Task<NotificationResult> AnswerNotificationAsync(Guid notificationId);

    /// <summary>
    /// Records that the notification's alert (toast, sound) has been presented, so it is not
    /// presented again.
    /// </summary>
    /// <param name="notificationId">The notification whose alert was presented.</param>
    /// <returns>
    /// <see cref="NotificationResultState.Updated"/>; <see cref="NotificationResultState.Unchanged"/>
    /// if already alerted; or <see cref="NotificationResultState.NotFound"/>.
    /// </returns>
    Task<NotificationResult> ConfirmAlertAsync(Guid notificationId);

    /// <summary>
    /// Posts a new event notification.
    /// </summary>
    /// <param name="target">
    /// Where to deliver it: a surface (<c>system.admin</c>), a user (<c>system.admin:jdoe</c>), or a
    /// group (<c>system.admin:g{managers}</c>). See <see cref="NotificationTarget"/>.
    /// </param>
    /// <param name="content">Text to display. Cannot be empty.</param>
    /// <param name="severity">How intrusively the receiving surface should present it.</param>
    /// <returns><see cref="NotificationResultState.Created"/> with the new notification's id.</returns>
    /// <exception cref="ArgumentException">The target is not well formed, or the content is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The severity is not a defined value.</exception>
    Task<NotificationResult> PostNotificationAsync(string target, string content, NotificationSeverity severity);

    /// <summary>
    /// Raises a condition notification, or refreshes it if one is already open for
    /// <paramref name="sourceKey"/>. Safe to call on every boot or check.
    /// </summary>
    /// <param name="sourceKey">
    /// Stable identifier for the condition, owned by the caller, e.g. <c>keyring.passthrough</c>.
    /// Compared ordinally (case-sensitive); at most 200 characters.
    /// </param>
    /// <param name="target">
    /// Where to deliver it, in the same form as <see cref="PostNotificationAsync"/>. Changing it on
    /// a later raise moves the open notification to the new target.
    /// </param>
    /// <param name="content">Text to display. Cannot be empty. A later raise may replace it.</param>
    /// <param name="severity">How intrusively the receiving surface should present it.</param>
    /// <returns>
    /// <see cref="NotificationResultState.Created"/> when no open notification existed;
    /// <see cref="NotificationResultState.Updated"/> when target, content, or severity changed (a
    /// higher severity or a new target also clears <c>AlertedAt</c> so the alert is presented
    /// again); otherwise <see cref="NotificationResultState.Unchanged"/>.
    /// </returns>
    /// <exception cref="ArgumentException">An argument is empty or not well formed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The severity is not a defined value.</exception>
    Task<NotificationResult> RaiseConditionAsync(
        string sourceKey, string target, string content, NotificationSeverity severity);

    /// <summary>
    /// Marks the open notification for <paramref name="sourceKey"/> as answered because the
    /// condition no longer holds.
    /// </summary>
    /// <param name="sourceKey">The key the condition was raised with.</param>
    /// <returns>
    /// <see cref="NotificationResultState.Updated"/> with its id, or
    /// <see cref="NotificationResultState.NotFound"/> (with <see cref="Guid.Empty"/>) if none was open.
    /// </returns>
    /// <exception cref="ArgumentException">The source key is empty or longer than 200 characters.</exception>
    Task<NotificationResult> ResolveConditionAsync(string sourceKey);
}

/// <summary>Outcome of a notification write.</summary>
/// <param name="Id">The affected notification, or <see cref="Guid.Empty"/> when none was found.</param>
/// <param name="State">What happened.</param>
public readonly record struct NotificationResult(Guid Id, NotificationResultState State)
{
    /// <summary>True for <see cref="NotificationResultState.Created"/>, <see cref="NotificationResultState.Updated"/>, and <see cref="NotificationResultState.Unchanged"/>.</summary>
    public bool Succeeded => State is NotificationResultState.Created
        or NotificationResultState.Updated
        or NotificationResultState.Unchanged;
}

public enum NotificationResultState
{
    /// <summary>A new notification was stored.</summary>
    Created,

    /// <summary>An existing notification was changed.</summary>
    Updated,

    /// <summary>The request was valid but already satisfied; nothing was written.</summary>
    Unchanged,

    /// <summary>No matching notification exists.</summary>
    NotFound
}

public enum NotificationSeverity
{
    /// <summary>
    /// Least severe notification, usually presented to the user in the least intrusive way such as a toast or a
    /// silent notification
    ///
    /// EX: A staff terminal lost connection during business hours but restored the connection before a more
    /// severe notice was warranted. The system sees the terminal lose communication then come back. The
    /// system sends an Info notification. If the admin is logged in, a toast would display and a notification
    /// would be displayed in the action center. No notification sound is sent.
    /// </summary>
    Info,

    /// <summary>
    /// Moderate notification, likely needs action ASAP. Typically presented to the user in a notification area
    /// with an audible cue to draw user attention.
    ///
    /// EX: A business day's schedule is being overridden by a schedule exception (holiday). System posts an
    /// Issue notification, the admin dashboard would then show a notification and play a notification sound
    /// on next admin login. If the admin is logged in, a toast would display and a notification
    /// would be displayed in the action center
    /// </summary>
    Issue,

    /// <summary>
    /// Extreme severity! This type of notification should be reserved for the most extreme cases that would
    /// cause serious system or workflow process disruption if not addressed immediately.
    ///
    /// EX: A secret vault is running in unsecured mode in the production environment. A Critical notification is sent
    /// to the system.admin target; that notification would be displayed as a warning banner in the admin panel
    /// across every page until addressed
    /// </summary>
    Critical
}
