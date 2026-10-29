using System.Text.Json.Serialization;

namespace OpenOrderSystem.Core.Services.Ordering.Timekeeping;

/// <summary>
/// One day's ordering hours, stored as a list under <see cref="OrderingConfigKeys.WeeklyHours"/>.
/// Slots run from <see cref="Open"/> up to (not including) <see cref="Close"/>. Hours that cross midnight
/// aren't supported; a day whose <see cref="Close"/> isn't after <see cref="Open"/> is treated as closed.
/// </summary>
public record OrderingDayHours(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DayOfWeek Day,
    TimeOnly Open,
    TimeOnly Close,
    bool IsClosed = false);
