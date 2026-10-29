# Cart Types

The draft structure stored in `Cart.DraftOrder`, plus the input and result types used by the cart services.

## `CartDraft`

`Services/Ordering/CartServices/Dto/CartDraft.cs`. The whole cart-in-progress, serialized as JSON into `Cart.DraftOrder`.

| Field | Meaning | Becomes (at placement) |
|---|---|---|
| `Customer` (`CartCustomerInfo?`) | Null until checkout. Submission requires it. | `CustomerRecord` |
| `OrderComments` | Order-level instructions | `OrderHeader.OrderComments` |
| `RequestedTimeSlot` (`DateTimeOffset?`) | Requested pickup time; `null` = ASAP | Resolved to `OrderHeader.AssignedTimeSlot` |
| `Lines` (`List<CartDraftLine>`) | Line items | `OrderLine`s |

## `CartDraftLine`

| Field | Meaning |
|---|---|
| `LineId` | Stable ID used by the remove/update/options methods. Generated when the line is added. |
| `ProductId`, `VariantId` | References to the live catalog (not FKs). At placement, `ProductId` is used to look up the snapshot. |
| `ProductName`, `VariantName` | Frozen when the line is added |
| `BasePrice` | Variant price, frozen when the line is added. **Excludes** options |
| `Quantity` | Units |
| `LineComments` | Line-level instructions |
| `SelectedOptions` (`List<CartDraftOption>`) | Option selections |

## `CartDraftOption`

| Field | Meaning |
|---|---|
| `OptionId` | Reference to the live option |
| `OptionName` | Frozen |
| `Delta` | The option's price **on this product** (`PriceDeltaOverride`, else the option's `PriceDelta`), frozen |
| `EffectiveDelta` | The amount that actually counts toward the line total: `Delta` for `Added`, `0` for `Included` and `Removed`. Frozen |
| `Quantity` | `0` = removed, `0.5` = light, `1` = standard, `>1` = extra |
| `SelectionState` | `Included`, `Added` or `Removed` (`OptionSelectionState`, shared with `OrderLineOption`) |

### Line total

```
unitPrice = BasePrice + Σ SelectedOptions.EffectiveDelta
lineTotal = unitPrice × Quantity
```

Option `Quantity` is not part of this formula today. If "extra" should cost more, the pricing rule
still needs to be decided.

## `CartCustomerInfo`

`Name`, `Phone`, `Email`, `NotificationPreferences` (`CustomerNotificationPreferences` flags).

## `CartLineOptionRequest`

Input to `SetLineOptionsAsync`. Expresses only what the customer wants: "I want this option (at this
quantity)" or, with `Removed` or quantity 0, "take this default off". The service decides whether an option
is included or added, and what it costs, from the catalog
(see [ICartService.md](ICartService.md#setlineoptionsasynccartid-lineid-ienumerablecartlineoptionrequest-options)).

| Field | Default |
|---|---|
| `OptionId` | — |
| `Quantity` | `1` |
| `SelectionState` | `Added` |

## `CartLookupResult`

`Services/Ordering/CartServices/CartLookupResult.cs`. Returned by every cart read and write. Check `Status` before using `Cart`.

| `Status` | `Cart` | Meaning |
|---|---|---|
| `Found` | Set | Active cart (after the change, for writes) |
| `NotFound` | `null` | No such cart, **or** a referenced line, product or variant is missing |
| `Disposed` | Set | Submitted or cancelled. A non-null `Cart.OrderConfirmation` means an order was created |
| `Rejected` | Set (unchanged) | The cart is active but the change was refused. `Message` holds a customer-presentable reason |

## `CartMetadataResetFlags`

`[Flags]` enum used with `UpdateCartMetadataAsync`. Each set bit turns a `null` argument into "clear this field".

| Flag | Clears |
|---|---|
| `None` | Nothing (`null` = unchanged) |
| `Customer` | `CartDraft.Customer` |
| `OrderComments` | `CartDraft.OrderComments` |
| `RequestedTimeSlot` | `CartDraft.RequestedTimeSlot` (back to ASAP) |
