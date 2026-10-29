# IOrderPlacementService

`Services/Ordering/Placement/`, implemented by `OrderPlacementService` (scoped).

Turns a cart into a queued order. The only writer of new `OrderHeader`, `OrderLine`, `CustomerRecord` and
`OrderQueue` rows.

## `PlaceOrderAsync(Guid cartId, Guid entryStageId, string orderOrigin)` → `OrderPlacementResult`

| Parameter | Meaning |
|---|---|
| `cartId` | The cart to submit. It must hold a slot (`ITimekeepingService.HoldSlotAsync`). |
| `entryStageId` | The stage the order enters when promoted. Passed explicitly because an instance can run several flows. |
| `orderOrigin` | The ordering channel, set by the API from the API key used; `"legacy"` for the legacy shim. |

### Steps

1. **Cart:** must be active. A disposed cart with a confirmation number returns `AlreadyPlaced` with the
   existing numbers (duplicate submit); a cancelled one returns `CartNotFound`.
2. **Draft:** needs customer details with a name, and at least one line (`CartIncomplete`).
3. **Open:** `ITimekeepingService.IsAcceptingOrdersAsync()` (`NotAcceptingOrders`).
4. **Claim the slot hold** (`SlotUnavailable` if missing, expired, or already being used by another submission).
5. **Entry stage** must exist (`StageNotFound`).
6. **Snapshot IDs:** each product's current snapshot via `FindProduct(IncludeSnapshot | IncludeArchived)`.
   Archived products still resolve, so archiving mid-session doesn't block checkout. No snapshot → `SnapshotMissing`.
7. **Order number:** next number for today's business day (see [OrderNumbers](../OrderNumbers/README.md)).
   Past 999 → `DailyLimitReached`.
8. **Save in one transaction:** `CustomerRecord`, `OrderHeader` (`IsQueued`, no current stage,
   `AssignedTimeSlot` = held slot), one `OrderLine` per cart line, and an `OrderQueue` entry
   (`ScheduledFor` = slot, `EntryStageId`).
9. **Finish:** complete the hold, store the confirmation number on the cart, dispose the cart.

Any failure from step 5 on unclaims the hold, so the customer keeps their slot and can retry. Nothing is written
unless step 8 succeeds.

### What's copied from the cart

| Order field | From |
|---|---|
| `OrderLine.ProductName`, `VariantName`, `Quantity`, `LineComments` | Cart line, as frozen |
| `OrderLine.UnitPrice` | `BasePrice + Σ EffectiveDelta` of the line's options |
| `OrderLine.SelectedOptions` | Each cart option, including `Included` and `Removed` defaults, so tickets show the full build |
| `OrderHeader.OrderComments` | Draft `OrderComments` |
| `CustomerRecord` | Draft `Customer` |

Prices are **not** rechecked against the catalog, and neither is availability: the customer pays what they saw,
and staff handle "just ran out" in person. This is an open design question.

### Result

`OrderPlacementResult`: `Status`, `OrderId`, `OrderNumber`, `ConfirmationNumber`, `AssignedTimeSlot`, `Message`.
Give the customer **`ConfirmationNumber`**, never `OrderNumber`. `Message` is customer-presentable on every failure.

| Status | Meaning |
|---|---|
| `Placed` | Order created, cart closed |
| `AlreadyPlaced` | Cart was already submitted; existing numbers returned, nothing created |
| `CartNotFound` | No such cart, or it was cancelled |
| `CartIncomplete` | No customer name, or no lines |
| `NotAcceptingOrders` | Ordering is closed |
| `SlotUnavailable` | No valid hold |
| `StageNotFound` | Bad `entryStageId` |
| `SnapshotMissing` | A product has no snapshot (shouldn't happen) |
| `DailyLimitReached` | 999 orders already today |
| `StorageError` | Database failure; nothing saved, hold kept |

## Known issues

- If closing the cart fails after the order is saved, the order stands but that cart loses duplicate-submit
  detection. This is logged as an error.
