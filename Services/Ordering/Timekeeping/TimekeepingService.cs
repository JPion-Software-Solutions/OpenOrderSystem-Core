using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.Timekeeping;

/// <summary>
/// Default singleton implementation of <see cref="ITimekeepingService"/>.
/// </summary>
public class TimekeepingService : ITimekeepingService
{
    private sealed record HoldEntry(SlotHold Hold, TimeSpan Duration, bool IsClaimed);

    private readonly ConcurrentDictionary<Guid, HoldEntry> _holds = new();
    // Serializes hold placement so two carts can't both take the last space in a slot.
    private readonly SemaphoreSlim _holdGate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly ILogger<TimekeepingService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TimekeepingService"/>.
    /// </summary>
    /// <remarks>
    /// The config store is scoped, so it is resolved from a short-lived scope rather than captured.
    /// </remarks>
    public TimekeepingService(
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<OosDbContext> contextFactory,
        ILogger<TimekeepingService> logger)
    {
        _scopeFactory = scopeFactory;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAcceptingOrdersAsync()
        => IsAcceptingOrders(await LoadSettingsAsync(), DateTimeOffset.UtcNow);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SlotAvailability>> GetSlotsAsync(DateOnly businessDay)
        => await BuildSlotsAsync(await LoadSettingsAsync(), businessDay, DateTimeOffset.UtcNow, excludeCartId: null);

    /// <inheritdoc/>
    public async Task<SlotHoldResult> HoldSlotAsync(Guid cartId, DateTimeOffset? requested)
    {
        var settings = await LoadSettingsAsync();
        var now = DateTimeOffset.UtcNow;

        if (!IsAcceptingOrders(settings, now))
            return new SlotHoldResult(SlotHoldStatus.NotAcceptingOrders, Message: "We aren't accepting orders right now.");

        await _holdGate.WaitAsync();
        try
        {
            // The cart's existing hold is excluded so it doesn't count against the slot it's replacing.
            var slots = await BuildSlotsAsync(settings, settings.BusinessDayOf(now), now, excludeCartId: cartId);

            SlotAvailability? slot;
            if (requested is null)
            {
                slot = slots.FirstOrDefault(s => s.IsSelectable);
                if (slot is null)
                    return new SlotHoldResult(SlotHoldStatus.Unavailable, Message: "There are no pickup times left today.");
            }
            else
            {
                slot = slots.FirstOrDefault(s => s.Slot == requested.Value.ToUniversalTime());
                if (slot is null || slot.Slot - now < settings.LeadTime)
                    return new SlotHoldResult(SlotHoldStatus.InvalidSlot, Message: "That pickup time isn't available.");
                if (slot.Remaining <= 0)
                    return new SlotHoldResult(SlotHoldStatus.Unavailable, Message: "That pickup time is full.");
            }

            var hold = new SlotHold(cartId, slot.Slot, now.AddMinutes(settings.HoldMinutes));
            _holds[cartId] = new HoldEntry(hold, hold.ExpiresAt - now, IsClaimed: false);
            _logger.LogDebug("Cart '{CartId}' holds slot {Slot} until {ExpiresAt}.", cartId, hold.Slot, hold.ExpiresAt);
            return new SlotHoldResult(SlotHoldStatus.Held, hold);
        }
        finally
        {
            _holdGate.Release();
        }
    }

    /// <inheritdoc/>
    public SlotHold? GetHold(Guid cartId)
        => _holds.TryGetValue(cartId, out var entry) && IsLive(entry, DateTimeOffset.UtcNow) ? entry.Hold : null;

    /// <inheritdoc/>
    public SlotHold? ClaimHold(Guid cartId)
    {
        if (!_holds.TryGetValue(cartId, out var entry) || entry.IsClaimed || !IsLive(entry, DateTimeOffset.UtcNow))
            return null;
        // Compare-and-swap: only one caller can move this exact entry to claimed.
        return _holds.TryUpdate(cartId, entry with { IsClaimed = true }, entry) ? entry.Hold : null;
    }

    /// <inheritdoc/>
    public void CompleteHold(Guid cartId) => _holds.TryRemove(cartId, out _);

    /// <inheritdoc/>
    public void UnclaimHold(Guid cartId)
    {
        if (_holds.TryGetValue(cartId, out var entry) && entry.IsClaimed)
        {
            var renewed = entry.Hold with { ExpiresAt = DateTimeOffset.UtcNow + entry.Duration };
            _holds.TryUpdate(cartId, entry with { Hold = renewed, IsClaimed = false }, entry);
        }
    }

    /// <inheritdoc/>
    public void ReleaseHold(Guid cartId) => _holds.TryRemove(cartId, out _);

    /// <inheritdoc/>
    public async Task<TimeSpan> GetLeadTimeAsync() => (await LoadSettingsAsync()).LeadTime;

    private async Task<OrderingSettings> LoadSettingsAsync()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await OrderingSettings.LoadAsync(
            scope.ServiceProvider.GetRequiredService<IConfigurationStore>(),
            scope.ServiceProvider.GetRequiredService<ITimeNormalizationService>());
    }

    private bool IsAcceptingOrders(OrderingSettings settings, DateTimeOffset now)
    {
        if (!settings.AcceptingOrders || !IsStaffTerminalOnline())
            return false;

        var local = TimeZoneInfo.ConvertTime(now, settings.TimeZone);
        var hours = settings.HoursFor(local.DayOfWeek);
        var time = TimeOnly.FromDateTime(local.DateTime);
        return hours is not null && time >= hours.Open && time < hours.Close;
    }

    /// <summary>
    /// Stub until the device communication service can report whether a staff terminal is online.
    /// </summary>
    private static bool IsStaffTerminalOnline() => true;

    private async Task<IReadOnlyList<SlotAvailability>> BuildSlotsAsync(
        OrderingSettings settings, DateOnly businessDay, DateTimeOffset now, Guid? excludeCartId)
    {
        var hours = settings.HoursFor(businessDay.DayOfWeek);
        if (hours is null || settings.SlotPeriodMinutes <= 0)
            return [];

        var period = TimeSpan.FromMinutes(settings.SlotPeriodMinutes);
        var starts = new List<DateTimeOffset>();
        for (var t = hours.Open.ToTimeSpan(); t < hours.Close.ToTimeSpan(); t += period)
        {
            if (settings.LocalToInstant(businessDay, TimeOnly.FromTimeSpan(t)) is { } instant)
                starts.Add(instant);
        }
        if (starts.Count == 0)
            return [];

        var placed = await CountPlacedOrdersAsync(starts[0], starts[^1]);
        var held = _holds.Values
            .Where(e => IsLive(e, now) && e.Hold.CartId != excludeCartId)
            .GroupBy(e => e.Hold.Slot)
            .ToDictionary(g => g.Key, g => g.Count());

        return starts.Select(slot =>
        {
            var remaining = settings.SlotsPerPeriod - placed.GetValueOrDefault(slot) - held.GetValueOrDefault(slot);
            return new SlotAvailability(slot, settings.SlotsPerPeriod, Math.Max(remaining, 0),
                IsSelectable: remaining > 0 && slot - now >= settings.LeadTime);
        }).ToList();
    }

    private async Task<Dictionary<DateTimeOffset, int>> CountPlacedOrdersAsync(DateTimeOffset first, DateTimeOffset last)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => o.AssignedTimeSlot >= first && o.AssignedTimeSlot <= last)
            .Select(o => new { o.AssignedTimeSlot, o.Flags })
            .ToListAsync();

        return orders
            .Where(o => !o.Flags.HasFlag(OrderHeaderFlags.IsCanceled))
            .GroupBy(o => o.AssignedTimeSlot.ToUniversalTime())
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static bool IsLive(HoldEntry entry, DateTimeOffset now) => entry.IsClaimed || entry.Hold.ExpiresAt > now;
}
