using System.Text.Json;
using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;
using OpenOrderSystem.Core.Rules;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// Parses and checks an <see cref="EventRule"/>'s version and JSON body: the trigger, and each action
/// against the <see cref="IRuleActionCatalog"/>.
/// </summary>
/// <remarks>
/// <para>
/// The trigger is handed to the shared <see cref="ConditionParser"/> at root path <c>trigger</c>; this
/// parser adds the event-bus-specific parts around it. It does not touch the database, so it cannot
/// tell whether a target device exists. <see cref="IEventRuleValidationService"/> adds that check, plus
/// checks on the rule's other fields.
/// </para>
/// <para>
/// <see cref="TryParse(IRuleActionCatalog, int, string, RuleErrorList)"/> is the non-throwing core. It
/// reports every problem it finds and returns whatever parsed cleanly. The <c>Parse</c> overloads wrap
/// it for callers that just want a complete result or an exception.
/// </para>
/// </remarks>
public static class EventRuleParser
{
    /// <summary>
    /// The only rule body version this parser reads.
    /// </summary>
    public const int SupportedRulesVersion = 1;

    private static readonly string[] BodyKeys = ["trigger", "actions"];
    private static readonly string[] ActionKeys = ["action", "target", "settings"];

    /// <summary>
    /// Parses a rule body, adding every problem it finds to <paramref name="errors"/>. Never throws for bad
    /// input.
    /// </summary>
    /// <remarks>
    /// <para>Checks, reported at the paths shown:</para>
    /// <list type="bullet">
    /// <item><c>rulesVersion</c>: must be <see cref="SupportedRulesVersion"/>. Otherwise the body's format is unknown, so it is not parsed.</item>
    /// <item>The body must be valid JSON (reported at the empty path, and then nothing else is parsed) and an object.</item>
    /// <item>Top-level keys other than <c>trigger</c> and <c>actions</c> are rejected.</item>
    /// <item><c>trigger</c>: required, and checked by <see cref="ConditionParser.TryParse(JsonElement, string, RuleErrorList)"/>.</item>
    /// <item><c>actions</c>: required, an array with at least one entry.</item>
    /// <item><c>actions[n]</c>: an object with only <c>action</c>, <c>target</c> and <c>settings</c>.</item>
    /// <item><c>actions[n].action</c>: a key in the catalog.</item>
    /// <item><c>actions[n].target</c>: a non-blank string; required for device actions and not allowed for service actions.</item>
    /// <item>
    /// <c>actions[n].settings</c>: a JSON object if present. For an action without a
    /// <see cref="RuleActionDescriptor.SettingsType"/>, absent or empty. Otherwise it must deserialize
    /// into that type with <see cref="RuleActionSettings.StrictOptions"/>, and any
    /// <see cref="IValidatableSettings"/> problems are reported at <c>actions[n].settings.&lt;property&gt;</c>
    /// (or <c>actions[n].settings</c> for a problem with the whole object).
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="catalog">The registered actions.</param>
    /// <param name="rulesVersion">The body's schema version, from <see cref="EventRule.RulesVersion"/>.</param>
    /// <param name="body">The body JSON, from <see cref="EventRule.Rules"/>.</param>
    /// <param name="errors">The list errors are added to. Errors already in it are left alone.</param>
    /// <returns>
    /// Whatever parsed cleanly: the trigger if it had no errors, and every action entry whose key, target
    /// and settings were valid, even if other parts of the rule failed. Nothing when the version is
    /// unsupported or the body is not valid JSON.
    /// </returns>
    public static ParsedEventRule TryParse(IRuleActionCatalog catalog, int rulesVersion, string body, RuleErrorList errors)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(errors);

        if (rulesVersion != SupportedRulesVersion)
        {
            errors.Add("rulesVersion", $"Rules version {rulesVersion} is not supported. The only supported version is {SupportedRulesVersion}.");
            return ParsedEventRule.Empty;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            errors.Add("", $"The rule body is not valid JSON: {ex.Message}");
            return ParsedEventRule.Empty;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                errors.Add("", $"The rule body must be a JSON object with \"trigger\" and \"actions\", but was {RuleJson.Describe(root.ValueKind)}.");
                return ParsedEventRule.Empty;
            }

            RuleJson.CheckKeys(root, "", BodyKeys, errors);

            ConditionNode? trigger = null;
            if (root.TryGetProperty("trigger", out var triggerElement))
                trigger = ConditionParser.TryParse(triggerElement, "trigger", errors);
            else
                errors.Add("trigger", "Required: a rule needs a \"trigger\" condition tree.");

            var actions = ParseActions(catalog, root, errors);

            return new ParsedEventRule(trigger, actions);
        }
    }

    /// <summary>
    /// Parses <paramref name="rule"/>'s <see cref="EventRule.RulesVersion"/> and <see cref="EventRule.Rules"/>,
    /// adding every problem it finds to <paramref name="errors"/>. See
    /// <see cref="TryParse(IRuleActionCatalog, int, string, RuleErrorList)"/>.
    /// </summary>
    public static ParsedEventRule TryParse(IRuleActionCatalog catalog, EventRule rule, RuleErrorList errors)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return TryParse(catalog, rule.RulesVersion, rule.Rules, errors);
    }

    /// <summary>
    /// Parses a rule body, throwing if it has any problems.
    /// </summary>
    /// <returns>The complete result. <see cref="ParsedEventRule.Trigger"/> is never <see langword="null"/>.</returns>
    /// <exception cref="RuleParseException">Thrown, carrying every problem found, if the body is invalid.</exception>
    public static ParsedEventRule Parse(IRuleActionCatalog catalog, int rulesVersion, string body)
    {
        var errors = new RuleErrorList();
        var parsed = TryParse(catalog, rulesVersion, body, errors);
        return errors.Count == 0 ? parsed : throw new RuleParseException(errors);
    }

    /// <summary>
    /// Parses <paramref name="rule"/>'s version and body, throwing if they have any problems.
    /// </summary>
    /// <returns>The complete result. <see cref="ParsedEventRule.Trigger"/> is never <see langword="null"/>.</returns>
    /// <exception cref="RuleParseException">Thrown, carrying every problem found, if the body is invalid.</exception>
    public static ParsedEventRule Parse(IRuleActionCatalog catalog, EventRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return Parse(catalog, rule.RulesVersion, rule.Rules);
    }

    private static List<ParsedRuleAction> ParseActions(IRuleActionCatalog catalog, JsonElement root, RuleErrorList errors)
    {
        const string path = "actions";
        var parsed = new List<ParsedRuleAction>();

        if (!root.TryGetProperty(path, out var actions))
        {
            errors.Add(path, "Required: a rule needs at least one action.");
            return parsed;
        }

        if (actions.ValueKind != JsonValueKind.Array)
        {
            errors.Add(path, $"Expected an array of actions, but found {RuleJson.Describe(actions.ValueKind)}.");
            return parsed;
        }

        if (actions.GetArrayLength() == 0)
            errors.Add(path, "A rule needs at least one action.");

        var index = 0;
        foreach (var entry in actions.EnumerateArray())
        {
            if (ParseAction(catalog, entry, index, errors) is { } action)
                parsed.Add(action);
            index++;
        }

        return parsed;
    }

    /// <summary>
    /// Checks one action entry. Returns it only if it named a known action and this entry added no errors.
    /// </summary>
    private static ParsedRuleAction? ParseAction(IRuleActionCatalog catalog, JsonElement entry, int index, RuleErrorList errors)
    {
        var path = RulePath.Index("actions", index);
        var before = errors.Count;

        if (entry.ValueKind != JsonValueKind.Object)
        {
            errors.Add(path, $"Expected an action object with \"action\", and optionally \"target\" and \"settings\", but found {RuleJson.Describe(entry.ValueKind)}.");
            return null;
        }

        RuleJson.CheckKeys(entry, path, ActionKeys, errors);

        var descriptor = ReadAction(catalog, entry, path, errors);
        var target = ReadTarget(entry, path, descriptor, errors);
        var settings = ReadSettings(entry, path, descriptor, errors);

        return descriptor is not null && errors.Count == before
            ? new ParsedRuleAction(index, descriptor, target, settings)
            : null;
    }

    private static RuleActionDescriptor? ReadAction(IRuleActionCatalog catalog, JsonElement entry, string path, RuleErrorList errors)
    {
        var actionPath = RulePath.Property(path, "action");

        if (!entry.TryGetProperty("action", out var action))
        {
            errors.Add(actionPath, "Required: an action entry needs an \"action\" key naming a registered action.");
            return null;
        }

        if (action.ValueKind != JsonValueKind.String)
        {
            errors.Add(actionPath, $"Expected a string, but found {RuleJson.Describe(action.ValueKind)}.");
            return null;
        }

        var key = action.GetString()!;
        if (string.IsNullOrWhiteSpace(key))
        {
            errors.Add(actionPath, "Must not be blank.");
            return null;
        }

        if (!catalog.TryGet(key, out var descriptor))
        {
            errors.Add(actionPath, $"Unknown action \"{key}\".");
            return null;
        }

        return descriptor;
    }

    private static string? ReadTarget(JsonElement entry, string path, RuleActionDescriptor? descriptor, RuleErrorList errors)
    {
        var targetPath = RulePath.Property(path, "target");
        var hasTarget = entry.TryGetProperty("target", out var target);

        if (descriptor?.Kind == RuleActionKind.Service && hasTarget)
        {
            errors.Add(targetPath, $"Not allowed: \"{descriptor.Key}\" is a service action and does not target a device. Remove \"target\".");
            return null;
        }

        if (!hasTarget)
        {
            if (descriptor?.Kind == RuleActionKind.Device)
                errors.Add(targetPath, $"Required: \"{descriptor.Key}\" is a device action and needs the key of a \"{descriptor.TargetDeviceTypeKey}\" device to target.");
            return null;
        }

        if (target.ValueKind != JsonValueKind.String)
        {
            errors.Add(targetPath, $"Expected a device key string, but found {RuleJson.Describe(target.ValueKind)}.");
            return null;
        }

        var key = target.GetString()!;
        if (string.IsNullOrWhiteSpace(key))
        {
            errors.Add(targetPath, "Must not be blank.");
            return null;
        }

        return key;
    }

    /// <summary>
    /// Reads and checks the entry's settings. Returns them as an object (an empty one when omitted).
    /// </summary>
    private static JsonElement ReadSettings(JsonElement entry, string path, RuleActionDescriptor? descriptor, RuleErrorList errors)
    {
        var settingsPath = RulePath.Property(path, "settings");
        var settings = RuleActionSettings.EmptyObject;
        var hasSettings = entry.TryGetProperty("settings", out var present);

        if (hasSettings)
        {
            if (present.ValueKind != JsonValueKind.Object)
            {
                errors.Add(settingsPath, $"Expected a JSON object, but found {RuleJson.Describe(present.ValueKind)}.");
                return settings;
            }
            settings = present.Clone();
        }

        // Without a known action there is nothing to check the settings against.
        if (descriptor is null) return settings;

        if (descriptor.SettingsType is null)
        {
            if (hasSettings && settings.EnumerateObject().Any())
                errors.Add(settingsPath, $"Action \"{descriptor.Key}\" takes no settings. Remove \"settings\" or leave it empty.");
            return settings;
        }

        if (!RuleActionSettings.TryDeserialize(settings, descriptor.SettingsType, out var value, out var reason))
        {
            errors.Add(settingsPath, $"Settings for action \"{descriptor.Key}\" are invalid: {reason}");
            return settings;
        }

        foreach (var problem in RuleActionSettings.Validate(value))
        {
            var problemPath = string.IsNullOrEmpty(problem.Property)
                ? settingsPath
                : RulePath.Property(settingsPath, problem.Property);
            errors.Add(problemPath, problem.Message);
        }

        return settings;
    }
}
