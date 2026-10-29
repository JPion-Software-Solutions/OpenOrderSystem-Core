# Order & Confirmation Numbers

`Services/Ordering/OrderNumbers/`

Every order has two numbers. Both are `int`, so they fit the legacy API compatibility shim.

| Number | Where | Shown to |
|---|---|---|
| **Order number** (`OrderHeader.OrderNumber`) | Plain `xxxxxyyy`, unique index | Staff / legacy shim |
| **Confirmation number** | Scrambled order number; stored on `Cart.OrderConfirmation`, computed otherwise | The customer |

## Order number: `OrderNumber` (static helper)

`xxxxx` = business days since the fixed epoch **2025-01-01**; `yyy` = the order's sequence within that day (1–999).
Example: the 12th order on 2026-10-28 (day 665) is `665012`.

- **Business day** = the local date, shifted back by `ordering.endOfDay` (with a 03:00 end of day, an order at
  01:00 Tuesday belongs to Monday). An order counts toward the day it was **placed**, not its pickup day.
- **Allocation** (in placement): highest number in today's range + 1. If two placements pick the same number,
  the unique index rejects the second save and it retries (up to 3 attempts).
- **Limit:** 999 per business day, then placement returns `DailyLimitReached`.
- `OrderNumber.RangeFor(day)`, `BusinessDayOf(number)`, `SequenceOf(number)` compose and decompose numbers.
- The epoch must never change, or every stored number changes meaning.

## Confirmation number: `IConfirmationNumberCodec`

Implemented by `ConfirmationNumberCodec` (scoped).

| Method | Behavior |
|---|---|
| `ToConfirmationNumberAsync(orderNumber)` | Scrambles an order number |
| `ToOrderNumberAsync(confirmationNumber)` | Reverses it, for lookups |

- A keyed, reversible permutation of 0–99,999,999 (a 4-round Feistel network over two 4-digit halves, HMAC-SHA256
  round function). Every order number has exactly one confirmation number and vice versa, so nothing needs storing.
- Display it zero-padded to 8 digits; small values are valid.
- **Purpose:** stop a customer guessing a neighbouring order's confirmation number in the order tracker and seeing
  someone else's name and phone number. It isn't a security boundary. Customer PII is only kept for 24 hours by default.
- **Key:** `ordering.confirmation.key` in the config store, generated and **locked** on first use. Changing it
  would make every previously issued confirmation number resolve to the wrong order.
