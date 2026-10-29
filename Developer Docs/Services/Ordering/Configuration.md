# Ordering Configuration

All ordering settings live in the config store (`IConfigurationStore`); keys and defaults are constants in
`Services/Ordering/OrderingConfigKeys.cs`. The legacy `SiteConfig` schedule is **not** used.

Settings are read once per operation (`OrderingSettings.LoadAsync`), so a single operation sees consistent values.
The config store caches values for 2 minutes, so changes can take up to that long to apply.

| Key | Type | Default | Meaning |
|---|---|---|---|
| `ordering.hours.weekly` | list of `OrderingDayHours` | *none* | Ordering hours per weekday. **Until this is set, ordering is closed.** |
| `ordering.acceptingOrders` | bool | `true` | Manual switch that stops all ordering |
| `ordering.slots.perPeriod` | int | `4` | Maximum orders per slot (x) |
| `ordering.slots.periodMinutes` | int | `15` | Slot length (y) |
| `ordering.slots.leadMinutes` | int | `20` | Minimum time between placement and pickup (z). Also how early a queued order is promoted into its stage |
| `ordering.slots.holdMinutes` | int | `10` | How long a slot hold lasts before it expires |
| `ordering.endOfDay` | `TimeOnly` (`"HH:mm:ss"`) | `00:00:00` | Local time the business day rolls over. Affects order numbering |
| `ordering.confirmation.key` | base64 string | generated | Confirmation number scramble key. Generated and **locked** on first use; never change it (see [OrderNumbers](OrderNumbers/README.md)) |

Local time comes from `ITimeNormalizationService` (the `local_time_zone` key, default UTC).

## `ordering.hours.weekly` format

```json
[
  { "day": "Monday",   "open": "11:00:00", "close": "20:00:00" },
  { "day": "Sunday",   "open": "00:00:00", "close": "00:00:00", "isClosed": true }
]
```

- `day` is a weekday name. A missing day is closed.
- Slots run from `open` up to, but not including, `close`.
- Hours that cross midnight aren't supported; a day whose `close` isn't after `open` is treated as closed.
