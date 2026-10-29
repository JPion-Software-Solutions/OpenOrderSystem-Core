using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping;

namespace OpenOrderSystem.Core.Services.Ordering;

/// <summary>
/// A point-in-time read of every ordering setting, plus the business-day and local-time math built on them.
/// Load once per operation so a single operation sees consistent values.
/// </summary>
internal sealed record OrderingSettings(
    TimeZoneInfo TimeZone,
    IReadOnlyList<OrderingDayHours> WeeklyHours,
    bool AcceptingOrders,
    int SlotsPerPeriod,
    int SlotPeriodMinutes,
    int LeadMinutes,
    int HoldMinutes,
    TimeOnly EndOfDay)
{
    public TimeSpan LeadTime => TimeSpan.FromMinutes(LeadMinutes);

    public static async Task<OrderingSettings> LoadAsync(IConfigurationStore config, ITimeNormalizationService time)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(await time.GetLocalTimezoneId());
        return new OrderingSettings(
            timeZone,
            await config.GetConfigurationAsync<List<OrderingDayHours>>(OrderingConfigKeys.WeeklyHours) ?? [],
            await config.GetConfigurationAsync(OrderingConfigKeys.AcceptingOrders, OrderingConfigKeys.DefaultAcceptingOrders),
            await config.GetConfigurationAsync(OrderingConfigKeys.SlotsPerPeriod, OrderingConfigKeys.DefaultSlotsPerPeriod),
            await config.GetConfigurationAsync(OrderingConfigKeys.SlotPeriodMinutes, OrderingConfigKeys.DefaultSlotPeriodMinutes),
            await config.GetConfigurationAsync(OrderingConfigKeys.LeadMinutes, OrderingConfigKeys.DefaultLeadMinutes),
            await config.GetConfigurationAsync(OrderingConfigKeys.HoldMinutes, OrderingConfigKeys.DefaultHoldMinutes),
            await config.GetConfigurationAsync(OrderingConfigKeys.EndOfDay, OrderingConfigKeys.DefaultEndOfDay));
    }

    /// <summary>
    /// The business day an instant belongs to: its local date, shifted back by <see cref="EndOfDay"/>
    /// (with a 03:00 end of day, 01:00 Tuesday still belongs to Monday).
    /// </summary>
    public DateOnly BusinessDayOf(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, TimeZone);
        return DateOnly.FromDateTime(local.DateTime - EndOfDay.ToTimeSpan());
    }

    /// <summary>
    /// Converts a local wall-clock time to an instant, or <see langword="null"/> if that time doesn't exist
    /// (skipped by a daylight-saving transition).
    /// </summary>
    public DateTimeOffset? LocalToInstant(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (TimeZone.IsInvalidTime(local))
            return null;
        return new DateTimeOffset(local, TimeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>The configured hours for a weekday, or <see langword="null"/> when closed or unconfigured.</summary>
    public OrderingDayHours? HoursFor(DayOfWeek day)
    {
        var hours = WeeklyHours.FirstOrDefault(h => h.Day == day);
        return hours is null || hours.IsClosed || hours.Close <= hours.Open ? null : hours;
    }
}
