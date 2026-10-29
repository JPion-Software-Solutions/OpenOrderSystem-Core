namespace OpenOrderSystem.Core.Services.Ordering;

/// <summary>
/// Configuration store keys and defaults used by the ordering services.
/// </summary>
public static class OrderingConfigKeys
{
    /// <summary>Weekly ordering hours: a list of <see cref="Timekeeping.OrderingDayHours"/>. No default; orders are refused until set.</summary>
    public const string WeeklyHours = "ordering.hours.weekly";

    /// <summary>Manual switch that stops all ordering when <see langword="false"/>.</summary>
    public const string AcceptingOrders = "ordering.acceptingOrders";
    public const bool DefaultAcceptingOrders = true;

    /// <summary>Maximum orders per slot (x).</summary>
    public const string SlotsPerPeriod = "ordering.slots.perPeriod";
    public const int DefaultSlotsPerPeriod = 4;

    /// <summary>Slot length in minutes (y).</summary>
    public const string SlotPeriodMinutes = "ordering.slots.periodMinutes";
    public const int DefaultSlotPeriodMinutes = 15;

    /// <summary>Minimum minutes between placement and pickup (z). Also how early a queued order is promoted.</summary>
    public const string LeadMinutes = "ordering.slots.leadMinutes";
    public const int DefaultLeadMinutes = 20;

    /// <summary>Minutes a slot hold lasts before it expires.</summary>
    public const string HoldMinutes = "ordering.slots.holdMinutes";
    public const int DefaultHoldMinutes = 10;

    /// <summary>Local time at which the business day rolls over (a <see cref="TimeOnly"/>).</summary>
    public const string EndOfDay = "ordering.endOfDay";
    public static readonly TimeOnly DefaultEndOfDay = TimeOnly.MinValue;

    /// <summary>Base64 key for the confirmation number scramble. Generated and locked on first use.</summary>
    public const string ConfirmationKey = "ordering.confirmation.key";
}
