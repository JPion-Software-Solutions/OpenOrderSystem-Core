using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;
using OpenOrderSystem.Core.Rules;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// The <see cref="IEventRuleValidationService"/> that checks target devices against the database.
/// </summary>
/// <remarks>
/// <para>
/// Builds one error list from, in order:
/// </para>
/// <list type="number">
/// <item><c>eventKey</c>: <see cref="EventRule.EventKey"/> must not be blank.</item>
/// <item><c>flags</c>: <see cref="EventRule.Flags"/> must have a defined flow mode.</item>
/// <item>
/// Everything <see cref="EventRuleParser.TryParse(IRuleActionCatalog, EventRule, RuleErrorList)"/>
/// reports about the version and body.
/// </item>
/// <item>
/// <c>actions[n].target</c>: for every device action that parsed cleanly, a device with that
/// <see cref="Data.DataModels.V2.Devices.DeviceHead.Key"/> must exist and be of the action's target device
/// type. This runs even when other parts of the rule have errors, so they are all reported together.
/// </item>
/// </list>
/// </remarks>
public sealed class DbEventRuleValidationService(IRuleActionCatalog catalog, OosDbContext db)
    : IEventRuleValidationService
{
    private readonly IRuleActionCatalog _catalog = catalog;
    private readonly OosDbContext _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleError>> ValidateAsync(EventRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var errors = new RuleErrorList();

        if (string.IsNullOrWhiteSpace(rule.EventKey))
            errors.Add("eventKey", "Must not be blank.");

        if (!rule.Flags.HasValidFlowMode())
            errors.Add("flags", $"Flow mode {(int)rule.Flags.FlowMode()} is not defined. Use {nameof(EventRuleFlags.Continue)} (0), {nameof(EventRuleFlags.StopAfterPriority)} (1) or {nameof(EventRuleFlags.StopAfterRule)} (2).");

        var parsed = EventRuleParser.TryParse(_catalog, rule, errors);

        await CheckTargetsAsync(parsed, errors, ct);

        return errors;
    }

    /// <summary>
    /// Checks that each device action's target exists and has the action's target device type.
    /// </summary>
    private async Task CheckTargetsAsync(ParsedEventRule parsed, RuleErrorList errors, CancellationToken ct)
    {
        var deviceActions = parsed.Actions
            .Where(a => a.Action.Kind == RuleActionKind.Device && a.Target is not null)
            .ToList();

        if (deviceActions.Count == 0) return;

        var keys = deviceActions.Select(a => a.Target!).Distinct(StringComparer.Ordinal).ToList();

        var found = await _db.Devices
            .AsNoTracking()
            .Where(d => keys.Contains(d.Key))
            .Select(d => new { d.Key, d.DeviceType })
            .ToListAsync(ct);

        // Matched exactly: a database with a case-insensitive collation may return a key that differs only
        // in case, which is not the key the rule names.
        var deviceTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var device in found)
            deviceTypes.TryAdd(device.Key, device.DeviceType);

        foreach (var action in deviceActions)
        {
            var path = RulePath.Property(RulePath.Index("actions", action.Index), "target");

            if (!deviceTypes.TryGetValue(action.Target!, out var deviceType))
                errors.Add(path, $"No device has the key \"{action.Target}\".");
            else if (!string.Equals(deviceType, action.Action.TargetDeviceTypeKey, StringComparison.Ordinal))
                errors.Add(path, $"Device \"{action.Target}\" is a \"{deviceType}\" device, but action \"{action.Action.Key}\" needs a \"{action.Action.TargetDeviceTypeKey}\" device.");
        }
    }
}
