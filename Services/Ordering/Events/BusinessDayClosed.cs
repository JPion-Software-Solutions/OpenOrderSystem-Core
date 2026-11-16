using OpenOrderSystem.Core.Services.EventBus;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.Events;

/// <summary>
/// A business day ended. Rules on it typically print or email end-of-day reports.
/// </summary>
/// <remarks>
/// <para>
/// Defined ahead of its producer: no v2 end-of-day job raises it yet.
/// </para>
/// <para>
/// Raise it with <see cref="ISystemEventBus.RaiseOnceAsync{TEvent}"/>, using the business date in ISO
/// format (<c>BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)</c>, such as
/// <c>2026-11-16</c>) as the dedupe key, so a scheduled job that fires twice closes the day only once.
/// </para>
/// </remarks>
/// <param name="BusinessDate">
/// The business day that closed, from <see cref="OrderingSettings.BusinessDayOf(DateTimeOffset)"/>, which
/// accounts for the configured end of day: with a 03:00 end of day, 01:00 Tuesday still belongs to Monday.
/// </param>
[SystemEvent("business-day.closed",
    DisplayName = "Business Day Closed",
    Description = "A business day ended. Carries the business date.")]
public sealed record BusinessDayClosed(DateOnly BusinessDate) : ISystemEvent;
