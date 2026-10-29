using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.Ordering;
using OpenOrderSystem.Core.Services.Catalog;
using OpenOrderSystem.Core.Services.Catalog.Interfaces;
using OpenOrderSystem.Core.Services.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.CartServices;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Dto;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Placement.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping.Interfaces;

namespace OpenOrderSystem.Core.Services.Ordering.Placement;

/// <summary>
/// Default scoped implementation of <see cref="IOrderPlacementService"/>.
/// </summary>
public class OrderPlacementService : IOrderPlacementService
{
    // Two placements on the same business day can pick the same next number; the unique index on
    // OrderNumber rejects the second save, which then retries with a fresh number.
    private const int MaxNumberAttempts = 3;

    private readonly ICartService _carts;
    private readonly IReadOnlyCatalog _catalog;
    private readonly ITimekeepingService _timekeeping;
    private readonly IConfirmationNumberCodec _confirmation;
    private readonly IConfigurationStore _config;
    private readonly ITimeNormalizationService _time;
    private readonly IDbContextFactory<OosDbContext> _contextFactory;
    private readonly ILogger<OrderPlacementService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="OrderPlacementService"/>.
    /// </summary>
    public OrderPlacementService(
        ICartService carts,
        IReadOnlyCatalog catalog,
        ITimekeepingService timekeeping,
        IConfirmationNumberCodec confirmation,
        IConfigurationStore config,
        ITimeNormalizationService time,
        IDbContextFactory<OosDbContext> contextFactory,
        ILogger<OrderPlacementService> logger)
    {
        _carts = carts;
        _catalog = catalog;
        _timekeeping = timekeeping;
        _confirmation = confirmation;
        _config = config;
        _time = time;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OrderPlacementResult> PlaceOrderAsync(Guid cartId, Guid entryStageId, string orderOrigin)
    {
        var lookup = await _carts.GetAsync(cartId);
        switch (lookup.Status)
        {
            case CartLookupStatus.NotFound:
                return Fail(OrderPlacementStatus.CartNotFound, "We couldn't find your cart.");
            case CartLookupStatus.Disposed:
                return await AlreadyPlacedOrNotFoundAsync(lookup.Cart!);
        }

        var draft = ReadDraft(lookup.Cart!);
        if (draft?.Customer is null || string.IsNullOrWhiteSpace(draft.Customer.Name))
            return Fail(OrderPlacementStatus.CartIncomplete, "Please add your name and contact details.");
        if (draft.Lines.Count == 0)
            return Fail(OrderPlacementStatus.CartIncomplete, "Your cart is empty.");

        if (!await _timekeeping.IsAcceptingOrdersAsync())
            return Fail(OrderPlacementStatus.NotAcceptingOrders, "We aren't accepting orders right now.");

        // Claiming is atomic, so a double-submit can't place two orders from one hold.
        var hold = _timekeeping.ClaimHold(cartId);
        if (hold is null)
            return Fail(OrderPlacementStatus.SlotUnavailable, "Your pickup time is no longer reserved. Please choose a time again.");

        OrderPlacementResult result;
        try
        {
            result = await PlaceClaimedAsync(cartId, draft, hold, entryStageId, orderOrigin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cart '{CartId}' — order placement failed.", cartId);
            result = Fail(OrderPlacementStatus.StorageError, "Something went wrong placing your order. Please try again.");
        }

        if (result.Status != OrderPlacementStatus.Placed)
        {
            _timekeeping.UnclaimHold(cartId);
            return result;
        }

        _timekeeping.CompleteHold(cartId);
        await CloseCartAsync(cartId, result);
        return result;
    }

    private async Task<OrderPlacementResult> PlaceClaimedAsync(
        Guid cartId, CartDraft draft, SlotHold hold, Guid entryStageId, string orderOrigin)
    {
        await using (var db = await _contextFactory.CreateDbContextAsync())
        {
            if (!await db.OrderStages.AnyAsync(s => s.Id == entryStageId))
                return Fail(OrderPlacementStatus.StageNotFound, "Ordering isn't set up correctly. Please contact the store.");
        }

        var snapshotIds = new Dictionary<Guid, Guid>();
        foreach (var productId in draft.Lines.Select(l => l.ProductId).Distinct())
        {
            var product = (await _catalog.FindProduct(productId,
                ProductLocatorFlags.IncludeSnapshot | ProductLocatorFlags.IncludeArchived)).Results.SingleOrDefault();
            if (product?.CurrentSnapshot?.Id is not { } snapshotId)
            {
                _logger.LogError("Cart '{CartId}' — product '{ProductId}' has no snapshot.", cartId, productId);
                return Fail(OrderPlacementStatus.SnapshotMissing, "An item in your cart can't be ordered. Please contact the store.");
            }
            snapshotIds[productId] = snapshotId;
        }

        var settings = await OrderingSettings.LoadAsync(_config, _time);
        var now = DateTimeOffset.UtcNow;
        var (first, last) = OrderNumber.RangeFor(settings.BusinessDayOf(now));

        for (var attempt = 1; ; attempt++)
        {
            await using var db = await _contextFactory.CreateDbContextAsync();

            var highest = await db.Orders
                .Where(o => o.OrderNumber >= first && o.OrderNumber <= last)
                .MaxAsync(o => (int?)o.OrderNumber);
            var orderNumber = (highest ?? first - 1) + 1;
            if (orderNumber > last)
                return Fail(OrderPlacementStatus.DailyLimitReached, "We can't take any more orders today.");

            var order = BuildOrder(draft, hold, snapshotIds, entryStageId, orderOrigin, orderNumber, now, out var customer, out var queueEntry);
            db.CustomerRecords.Add(customer);
            db.Orders.Add(order);
            db.OrderQueue.Add(queueEntry);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException) when (attempt < MaxNumberAttempts)
            {
                if (!await NumberTakenAsync(orderNumber))
                    throw;
                _logger.LogDebug("Cart '{CartId}' — order number {OrderNumber} was taken concurrently; retrying.", cartId, orderNumber);
                continue;
            }

            _logger.LogInformation("Cart '{CartId}' placed as order {OrderNumber} for slot {Slot}.", cartId, orderNumber, hold.Slot);
            return new OrderPlacementResult(OrderPlacementStatus.Placed, order.Id, orderNumber,
                await _confirmation.ToConfirmationNumberAsync(orderNumber), hold.Slot);
        }
    }

    private static OrderHeader BuildOrder(
        CartDraft draft, SlotHold hold, IReadOnlyDictionary<Guid, Guid> snapshotIds, Guid entryStageId,
        string orderOrigin, int orderNumber, DateTimeOffset now, out CustomerRecord customer, out OrderQueue queueEntry)
    {
        customer = new CustomerRecord
        {
            Name = draft.Customer!.Name,
            Phone = draft.Customer.Phone,
            Email = draft.Customer.Email,
            NotificationPreferences = draft.Customer.NotificationPreferences,
            CustomerCreated = now
        };

        var order = new OrderHeader
        {
            OrderNumber = orderNumber,
            OrderDate = now,
            CustomerRecordId = customer.Id,
            OrderComments = draft.OrderComments,
            AssignedTimeSlot = hold.Slot,
            OrderOrigin = orderOrigin,
            Flags = OrderHeaderFlags.IsQueued,
            CurrentStageId = null
        };

        foreach (var line in draft.Lines)
        {
            var orderLine = new OrderLine
            {
                OrderHeaderId = order.Id,
                ProductSnapshotId = snapshotIds[line.ProductId],
                ProductId = line.ProductId,
                ProductName = line.ProductName,
                VariantName = line.VariantName,
                UnitPrice = line.BasePrice + line.SelectedOptions.Sum(o => o.EffectiveDelta),
                Quantity = line.Quantity,
                LineComments = line.LineComments
            };
            orderLine.SetOptions(line.SelectedOptions.Select(o =>
                new OrderLineOption(o.OptionName, o.Delta, o.EffectiveDelta, o.Quantity, o.SelectionState)));
            order.LineItems.Add(orderLine);
        }

        queueEntry = new OrderQueue
        {
            OrderHeaderId = order.Id,
            ScheduledFor = hold.Slot,
            EntryStageId = entryStageId
        };
        return order;
    }

    private async Task<bool> NumberTakenAsync(int orderNumber)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Orders.AnyAsync(o => o.OrderNumber == orderNumber);
    }

    private async Task<OrderPlacementResult> AlreadyPlacedOrNotFoundAsync(Cart cart)
    {
        if (cart.OrderConfirmation is not { } confirmation)
            return Fail(OrderPlacementStatus.CartNotFound, "This cart was cancelled.");

        var orderNumber = await _confirmation.ToOrderNumberAsync(confirmation);
        await using var db = await _contextFactory.CreateDbContextAsync();
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.OrderNumber == orderNumber)
            .Select(o => new { o.Id, o.AssignedTimeSlot })
            .FirstOrDefaultAsync();
        return new OrderPlacementResult(OrderPlacementStatus.AlreadyPlaced, order?.Id, orderNumber, confirmation, order?.AssignedTimeSlot);
    }

    private async Task CloseCartAsync(Guid cartId, OrderPlacementResult result)
    {
        // The order is already committed; a failure here only weakens duplicate-submit detection for this cart.
        try
        {
            await _carts.SetOrderNumberAsync(cartId, result.ConfirmationNumber!.Value);
            await _carts.DisposeAsync(cartId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderNumber} was placed but cart '{CartId}' could not be closed.", result.OrderNumber, cartId);
        }
    }

    private static CartDraft? ReadDraft(Cart cart)
    {
        if (string.IsNullOrWhiteSpace(cart.DraftOrder))
            return null;
        try { return JsonSerializer.Deserialize<CartDraft>(cart.DraftOrder); }
        catch (JsonException) { return null; }
    }

    private static OrderPlacementResult Fail(OrderPlacementStatus status, string message) => new(status, Message: message);
}
