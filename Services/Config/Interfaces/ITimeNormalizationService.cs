namespace OpenOrderSystem.Core.Services.Config.Interfaces;

/// <summary>
/// Converts between UTC and the system's configured local time zone, and manages that
/// configured time zone itself. "Local" here means the single site-wide zone used to
/// interpret and display order/report timestamps for staff (e.g. "what calendar day did
/// this order fall on, in the store's own time") — it is a configured value, independent
/// of whatever time zone the host machine itself happens to be set to.
/// </summary>
/// <remarks>
/// Time zone IDs are expected to be IANA zone IDs (e.g. <c>"America/Detroit"</c>), not
/// legacy Windows IDs (e.g. <c>"Eastern Standard Time"</c>). On .NET 6+ with ICU available
/// (the default for this project — see <c>InvariantGlobalization</c> in the .csproj),
/// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> resolves IANA IDs correctly on both
/// Windows and Linux hosts, so one configured ID works across every deployment target.
/// </remarks>
public interface ITimeNormalizationService
{
    /// <summary>
    /// Gets the current instant, converted into the configured local time zone.
    /// Equivalent to calling <see cref="ConvertToLocalTime"/> with <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown if the configured time zone ID cannot be resolved on this host. Should not
    /// happen in practice: <see cref="SetLocalTimezone"/> validates its input up front and
    /// throws there instead, specifically so a bad value can never reach storage and
    /// surface here later.
    /// </exception>
    public Task<DateTimeOffset> GetLocalTime();

    /// <summary>
    /// Converts <paramref name="date"/> to UTC.
    /// </summary>
    /// <remarks>
    /// Synchronous and performs no configuration lookup, unlike the local-time members of
    /// this interface: UTC is an absolute reference and never depends on the configured
    /// local time zone, so there is nothing to await here.
    /// </remarks>
    public DateTimeOffset ConvertToUTC(DateTimeOffset date);

    /// <summary>
    /// Converts <paramref name="date"/> — an absolute instant, carrying its own offset —
    /// into the configured local time zone.
    /// </summary>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown if the configured time zone ID cannot be resolved on this host. See
    /// <see cref="GetLocalTime"/> for why this can happen.
    /// </exception>
    public Task<DateTimeOffset> ConvertToLocalTime(DateTimeOffset date);

    /// <summary>
    /// Gets the raw configured local time zone ID (expected to be an IANA ID) without
    /// resolving it to a <see cref="TimeZoneInfo"/>.
    /// </summary>
    /// <remarks>
    /// Falls back to <see cref="TimeZoneInfo.Utc"/> if no value has ever been configured —
    /// a fixed, host-independent default, deliberately not the host machine's own
    /// <see cref="TimeZoneInfo.Local"/> zone. An unconfigured instance should surface as
    /// "obviously not set up" (UTC-displayed timestamps) rather than silently tracking
    /// whatever zone the server happens to be deployed in.
    /// </remarks>
    public Task<string> GetLocalTimezoneId();

    /// <summary>
    /// Validates and persists <paramref name="ianaTimeZone"/> as the site-wide configured
    /// local time zone.
    /// </summary>
    /// <param name="ianaTimeZone">
    /// An IANA time zone ID (e.g. <c>"America/Detroit"</c>).
    /// </param>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown if <paramref name="ianaTimeZone"/> does not resolve via
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>. Validated here, up front, so an
    /// invalid value can never reach storage and fail later instead, at read time.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the underlying write is rejected — in practice, only if the configured
    /// time zone's configuration key has somehow been marked locked (e.g. via direct
    /// database edits, or a future config-store admin UI; there is no supported path for
    /// an operator to lock it through normal use).
    /// </exception>
    public Task SetLocalTimezone(string ianaTimeZone);
}