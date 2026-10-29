# IOrderStageService

`Services/Ordering/Stages/`, implemented by `OrderStageService` (scoped). Driven by `Quartz/AutomatedTasks/OrderStageJob.cs`.

Moves orders out of the queue into their stage chain, then along it.

## Entering a stage

Every move (promotion or advance) does the same thing:
- Closes the open `StageHistory` entry (`ExitedAt = now`) and appends a new one.
- Sets `CurrentStageId`.
- Sets `StageAdvanceTime` to now + the stage's `AutoCompleteTime` (minutes), or `null` if the stage requires
  manual progression.
- If the stage is flagged **`IsTerminal`**, the order is marked **`IsFulfilled`**: it is complete, and no
  advance time is set.

Stage effects (ticket printing, sounds, customer notifications) are **not implemented yet**.

## Methods

| Method | Behavior |
|---|---|
| `PromoteDueOrdersAsync()` | Promotes every queued order where `ScheduledFor − leadTime <= now` into its `EntryStageId` stage: clears `IsQueued`, enters the stage, deletes the queue row. Returns the count. |
| `AdvanceDueOrdersAsync()` | Advances every active order whose `StageAdvanceTime` has passed to its stage's `NextStage`. If the stage has no next stage, the advance time is cleared (and a warning logged) so it isn't retried every tick. Returns the count. |
| `PromoteNowAsync(orderId)` | Staff early promotion. `NotQueued` if the order isn't in the queue. |
| `AdvanceNowAsync(orderId)` | Staff manual advance. `NotActive` if still queued, `AtFinalStage` if the stage has no next stage. |

The manual methods return `OrderStageResult` (`Status`, `OrderId`, `StageId` after the move, `Message`).

## The job

`OrderStageJob` runs both batch methods in turn, every 10 seconds, without overlapping itself
(`[DisallowConcurrentExecution]`). It isn't registered yet; it moves onto the scheduling service once that exists.

If a batch collides with a staff action on the same order (for example the queue row was already removed by an
early promotion), the whole batch is dropped and picked up again on the next run.

## Known issues

- **Last write wins** when the job and a staff action update the *same* order at the same moment without
  removing a row (for example both advancing it). The order can end up with a duplicate history entry.
- Cancelled orders aren't skipped yet; the cancel/comp/waste lifecycle isn't designed.
