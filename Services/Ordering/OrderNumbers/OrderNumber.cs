namespace OpenOrderSystem.Core.Services.Ordering.OrderNumbers;

/// <summary>
/// Composes and decomposes plain order numbers in the form <c>xxxxxyyy</c>: <c>xxxxx</c> is the number of
/// business days since <see cref="Epoch"/>, <c>yyy</c> the order sequence within that day.
/// </summary>
public static class OrderNumber
{
    /// <summary>Business day zero. Fixed forever: changing it would change the meaning of every stored number.</summary>
    public static readonly DateOnly Epoch = new(2025, 1, 1);

    public const int MaxSequence = 999;
    private const int MaxDayIndex = 99_999;

    /// <summary>The lowest and highest order numbers available on a business day.</summary>
    public static (int First, int Last) RangeFor(DateOnly businessDay)
    {
        var dayIndex = businessDay.DayNumber - Epoch.DayNumber;
        if (dayIndex is < 0 or > MaxDayIndex)
            throw new ArgumentOutOfRangeException(nameof(businessDay), businessDay, "Business day is outside the order number range.");
        return (dayIndex * 1000 + 1, dayIndex * 1000 + MaxSequence);
    }

    /// <summary>The business day encoded in an order number.</summary>
    public static DateOnly BusinessDayOf(int orderNumber) => Epoch.AddDays(orderNumber / 1000);

    /// <summary>The within-day sequence encoded in an order number.</summary>
    public static int SequenceOf(int orderNumber) => orderNumber % 1000;
}
