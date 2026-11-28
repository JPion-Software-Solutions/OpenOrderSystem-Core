# Scheduling

OOS jobs implement `IOosJob` and are found by scanning; nothing lists them. `IJobScheduleService`
(`QuartzJobScheduleService`, a singleton) owns their schedule. The schedule is never patched: every change
clears the Quartz group `"oos"` and registers every job again (`RebuildAsync`). The `PostBootTaskRunnerJob`
runs the first rebuild at boot, as the "Build Schedule" task.

## Two kinds of job

- **Always-on** jobs, such as `OrderingTickJob`, schedule themselves from the general config store with
  hard-coded defaults.
- **Optional** jobs, and jobs with several instances, are scheduled from the **schedule manifest**. Their
  `RegisterAsync` is one call:

  ```csharp
  [OosQuartzJob("ordering.daily-report", Name = "Daily report")]
  public class DailyReportJob : IOosJob
  {
      public static Task RegisterAsync(IServiceProvider services, IScheduler scheduler, CancellationToken ct) =>
          services.GetRequiredService<IJobScheduleService>().ScheduleFromManifestAsync<DailyReportJob>(services, scheduler, ct);
  }
  ```

  In `Execute`, `context.GetManifestInstanceId()` and `context.GetManifestSettings<TSettings>()` say which
  instance is running and with what settings.

## The manifest

`<data root>/config/schedule.json`. It can be edited by hand; changes apply on the next rebuild.

```json
{
  "version": 1,
  "jobs": {
    "ordering.daily-report": {
      "enabled": true,
      "instances": {
        "front-counter": { "enabled": true, "cron": "0 0 21 * * ?", "settings": { "recipients": ["manager@example.com"] } }
      }
    }
  }
}
```

- **Job keys** come from `[OosQuartzJob("key")]`, not the class name, so classes can be renamed. Never
  change a released key: entries under the old key are orphaned.
- **Instance IDs** are stable strings. Each instance becomes the Quartz job `<key>:<instance ID>`.
- **`cron`** is a Quartz cron expression (seconds first), read in the store's timezone (UTC if that can't
  be resolved). The timezone is taken at rebuild time, so the UI that changes it calls `RebuildAsync`
  afterwards. Missed fires run once as soon as possible.
- **`settings`** is any JSON object; each job reads it into its own type (camelCase, case-insensitive).
  `enabled` defaults to `true` at both levels.

### What happens when it's wrong

Nothing in the manifest can fail a rebuild or stop OOS:

| Problem | Result |
|---|---|
| No file | Only always-on jobs are scheduled (logged at Information). |
| Invalid JSON, empty file, wrong `version` | Logged at Error; only always-on jobs are scheduled. |
| Key matching no job | Logged at Warning and ignored. |
| Blank instance ID, missing or invalid `cron` | That instance is logged at Warning and skipped. |
| Key declared by two jobs | Neither is scheduled from the manifest; logged at Error. |

`ReadManifestAsync` is the exception: it throws on an unreadable file, so an editor never mistakes a broken
file for an empty one and saves over it. `SaveManifestAsync` writes the file in one step (temp file, then
rename) and rebuilds; it doesn't reject entries, and the rebuild logs and skips what it can't schedule.

## Known gaps

- **No cancellation for post-boot tasks.** The boot rebuild runs uncancelled.
- **Group membership isn't checked.** A job that schedules itself outside `"oos"` is never cleared, so it's
  duplicated on every rebuild. `ScheduleFromManifestAsync` always uses the right group.
- **Deleting a job doesn't stop a run in progress.** It can briefly overlap its replacement's first run.
