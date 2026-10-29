# Ordering

Takes a customer from an empty cart to a fulfilled order: carts, order placement, the pickup-time
queue, and stage advancement.

**Source:** `Services/Ordering/`  
**Models:** `Data/DataModels/V2/Ordering/`  
**Design:** [`Design Docs/OOS-Ordering-Service.md`](../../../../Design%20Docs/OOS-Ordering-Service.md) (status: design in progress)

## Implementation status

| Component | Status | Docs |
|---|---|---|
| Cart service and cache | **Built** | [CartServices](CartServices/README.md) |
| Timekeeping (slots, holds, ASAP, accepting orders) | **Built** | [Timekeeping](Timekeeping/README.md) |
| Order placement (cart → order) | **Built** | [Placement](Placement/README.md) |
| Order and confirmation numbers | **Built** | [OrderNumbers](OrderNumbers/README.md) |
| Queue promotion, auto-advance, staff promote/advance | **Built**; job not yet scheduled | [Stages](Stages/README.md) |
| Configuration keys | — | [Configuration.md](Configuration.md) |
| Stage entry/exit effects (tickets, sounds, notifications) | **Not built** (deferred with stage rules) | — |
| Stage setup | **Not built** (comes with initial setup) | — |
| Cancel / comp / waste | **Not designed** | — |
| V2 API | **Not built**; this slice stops at the service layer | — |

None of the V2 services are registered in DI yet. They will be registered together through one extension method.

## Domain models

All live in `Data/DataModels/V2/Ordering/` and already have tables in the V2 migrations.

| Model | Purpose |
|---|---|
| `Cart` | A persisted cart: JSON `DraftOrder`, `LastActive`, `IsDisposed`, `OrderConfirmation`. Managed only by the cart services. |
| `OrderHeader` | One order: `OrderNumber` (int, `xxxxxyyy`), `AssignedTimeSlot`, `CurrentStageId`, `StageAdvanceTime`, `Flags` (`IsCanceled`, `IsWaste`, `IsComp`, `IsQueued`, `IsFulfilled`), `OrderOrigin`, comments, metadata, owned `StageHistory`. |
| `OrderLine` | One line on an order, with its display data frozen: `ProductName`, `VariantName`, `UnitPrice`, `Quantity`, `SelectedOptionsJson` (`OrderLineOption` records), plus `ProductSnapshotId`. |
| `CustomerRecord` | Customer contact and notification preferences captured at placement. |
| `OrderQueue` | Orders waiting for their time slot: `OrderHeaderId`, `ScheduledFor` (the slot), `EntryStageId` (the stage it enters on promotion). |
| `OrderStage` | A user-defined workflow stage. Stages form a doubly-linked list (`PreviousStageId`/`NextStageId`) with `OrderStageFlags` (`IsInitial`, `IsTerminal`, `PrintsOrderTicket`, `PlaysAudibleSound`, `NotifiesCustomerOn*`, visibility flags, `CollapseUp`, …) and an optional `AutoCompleteTime`. |
| `OrderStageHistory` | Owned by `OrderHeader`: `StageId`, `EnteredAt`, `ExitedAt`. |

`TimeSlot.cs` also exists in this folder but is **not** in `OosDbContext`, and it stores holds as rows
(`HeldInCart`), which contradicts the design's in-memory holds. Treat it as dead code until
timekeeping is built.

## End-to-end flow

1. **Cart:** the client builds a draft through [`ICartService`](CartServices/ICartService.md). Names, prices and
   included options are frozen from the catalog as items are added.
2. **Pickup time:** [`ITimekeepingService`](Timekeeping/README.md) lists slots; the client holds one for the cart
   (or ASAP).
3. **Place:** [`IOrderPlacementService.PlaceOrderAsync`](Placement/README.md) writes the order in one transaction,
   queues it for its slot, and closes the cart. The customer gets the confirmation number.
4. **Promote:** the [order stage job](Stages/README.md) moves the order into its entry stage once its slot is within
   the lead time (or staff pull it in early).
5. **Advance:** stages advance on their timers or by staff, until the order enters a terminal stage and is
   marked fulfilled.

## Ownership rules (from the design)

- The ordering service is the **only writer** to `OrderHeader`, `OrderLine`, `CustomerRecord` and `OrderQueue`.
- Ordering **never creates snapshots**. It stamps the catalog's current snapshot ID onto each line.
- **Every order goes through the queue**, ASAP orders included.

## SQLite and `DateTimeOffset`

SQLite has no `DateTimeOffset` type, and EF can't translate comparisons or sorting on the default text storage.
`SqliteOosDbContext` (`Data/Providers/ProviderDbContexts.cs`) therefore stores every `DateTimeOffset` as a
sortable 64-bit integer (`DateTimeOffsetToBinaryConverter`), which keeps the original offset. SQL Server and
Postgres use their native types. Any new `DateTimeOffset` property gets this automatically.
