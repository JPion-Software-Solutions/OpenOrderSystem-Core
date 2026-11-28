using OpenOrderSystem.Core.Services.Ordering.Reporting.Dto;

namespace OpenOrderSystem.Core.Services.Ordering.Reporting.Interfaces;

/// <summary>
/// Scoped, read-only service that aggregates orders into reports. Looking up and laying out individual orders
/// belongs to <see cref="Queries.Interfaces.IOrderQueryService"/>.
/// </summary>
/// <remarks>
/// It only gathers figures. Formatting, sending and scheduling reports are up to its callers, such as the
/// ordering report job.
/// </remarks>
public interface IOrderReportingService
{
    /// <summary>The store's current business day, for building a <see cref="ReportPeriod"/>.</summary>
    Task<DateOnly> GetCurrentBusinessDayAsync();

    /// <summary>
    /// Builds the report for every order placed in <paramref name="period"/>.
    /// </summary>
    /// <param name="period">The business days to cover.</param>
    /// <returns>The report. A period with no orders gives a report of zeros and empty lists.</returns>
    /// <exception cref="ArgumentException"><see cref="ReportPeriod.LastDay"/> is before <see cref="ReportPeriod.FirstDay"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A day is outside the order number range.</exception>
    Task<OrderReport> GetOrderReportAsync(ReportPeriod period);
}
