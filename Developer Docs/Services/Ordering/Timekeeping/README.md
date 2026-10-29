# ITimekeepingService

`Services/Ordering/Timekeeping/`, implemented by `TimekeepingService` (**singleton**).

Pickup slots: which exist, how full they are, holding one for a cart, and whether ordering is open. Owns no
tables. Placed orders are counted from `OrderHeader.AssignedTimeSlot`; holds live in memory.
Settings: [Configuration.md](../Configuration.md).

## Slots

For a business day, slots start at the day's `open` time and repeat every `periodMinutes` up to (not including)
`close`. Each slot's capacity is `perPeriod`. A slot time skipped by a daylight-saving change is left out.

**Remaining capacity** = capacity − non-cancelled orders already in the slot − other carts' live holds.
A slot is **selectable** when capacity remains and it is at least `leadMinutes` away.

## Methods

| Method | Behavior |
|---|---|
| `IsAcceptingOrdersAsync()` | `true` when the switch is on, a staff terminal is online, and the current local time is within today's hours. The terminal check is a stub that always returns `true` until the device communication service can report it. |
| `GetSlotsAsync(DateOnly businessDay)` | Every slot for that day with `Capacity`, `Remaining` and `IsSelectable`. Times are UTC. Empty if the day is closed or hours aren't set. |
| `HoldSlotAsync(cartId, requested)` | Holds a slot for the cart, replacing any hold it already has. `requested = null` means ASAP: the first selectable slot today. Otherwise `requested` must be a slot start from `GetSlotsAsync` and at least the lead time away. Only today's business day can be held. |
| `GetHold(cartId)` | The cart's live hold, or `null`. |
| `ClaimHold(cartId)` / `CompleteHold` / `UnclaimHold` | Used by placement. See below. |
| `ReleaseHold(cartId)` | Drops the cart's hold. |
| `GetLeadTimeAsync()` | The configured lead time. |

`HoldSlotAsync` results: `Held` (with the hold and its expiry), `NotAcceptingOrders`, `InvalidSlot` (not a slot
today, or inside the lead time), or `Unavailable` (full, or no ASAP slot left). Every failure has a
customer-presentable `Message`.

## Holds

- One per cart, kept in memory and lost on restart. A customer whose hold disappears gets `SlotUnavailable` at
  checkout and simply picks a time again.
- Expire after `holdMinutes`. Expired holds stop counting immediately.
- Placing holds is serialized, so two carts can't both take the last space in a slot.
- **Claiming:** placement claims the hold before writing the order. A claimed hold can't be claimed again
  (blocking double submits), doesn't expire, and still counts against capacity. On success placement completes
  it (removes it); on failure it's unclaimed with a fresh expiry, so the customer keeps their slot and can retry.

## Typical flow

```csharp
var slots = await timekeeping.GetSlotsAsync(today);                    // show the customer selectable slots
var hold  = await timekeeping.HoldSlotAsync(cartId, chosenSlot);       // or null for ASAP
// ... customer finishes checkout ...
var result = await placement.PlaceOrderAsync(cartId, entryStageId, origin);   // consumes the hold
```

## Known issues

- **Single instance only.** Holds are in memory, like the cart cache.
