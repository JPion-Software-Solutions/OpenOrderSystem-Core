using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenOrderSystem.Core.Rules;
using OpenOrderSystem.Core.Services.EventBus.Interfaces;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// Reads the two parts of <see cref="RuleActionContext.Input"/> into a handler's own types: the rule's
/// settings with <see cref="TryReadSettings{TSettings}"/> and the event with
/// <see cref="TryReadEvent{TEvent}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Settings are read with <see cref="SettingsOptions"/> and the event with <see cref="EventOptions"/>, so
/// handlers only check what the type system can't express. Put those checks in the settings record's
/// <see cref="IValidatableSettings.Validate"/>, not in the handler, so rule validation catches them when
/// the rule is saved.
/// </para>
/// <para>
/// A failure from either method means the rule or the event is wrong, which retrying cannot fix, so
/// handlers should return <see cref="RuleActionResult.Fail"/>.
/// </para>
/// </remarks>
public static class RuleActionSettings
{
    /// <summary>
    /// The key of the event payload in <see cref="RuleActionContext.Input"/>.
    /// </summary>
    public const string EventKey = "event";

    /// <summary>
    /// The key of the rule's settings in <see cref="RuleActionContext.Input"/>.
    /// </summary>
    public const string SettingsKey = "settings";

    /// <summary>
    /// The options a rule's settings are read with: <see cref="EventOptions"/>, plus unknown keys are
    /// rejected (<see cref="JsonUnmappedMemberHandling.Disallow"/>) and numbers must be JSON numbers
    /// (<see cref="JsonNumberHandling.Strict"/>, so <c>"5"</c> is not accepted for a number setting).
    /// Read-only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Settings are written by a person editing a rule, so an unknown key is almost always a typo that would
    /// otherwise be silently ignored. Rejecting it surfaces the mistake when the rule is saved.
    /// </para>
    /// <para>
    /// Shared by rule validation (<see cref="EventRuleParser"/>) and by handlers reading their settings
    /// with <see cref="TryReadSettings{TSettings}"/>, so both accept and reject exactly the same settings.
    /// Use <see cref="EventOptions"/> for event payloads, never these.
    /// </para>
    /// </remarks>
    public static JsonSerializerOptions SettingsOptions { get; } = CreateSettingsOptions();

    /// <summary>
    /// The options an event payload is read with: <see cref="JsonSerializerOptions.Web"/> (camelCase,
    /// case-insensitive), made strict: every constructor parameter of a positional record must be present,
    /// and <see langword="null"/> is rejected for non-nullable properties instead of quietly getting
    /// through. Read-only.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="SettingsOptions"/>, unknown keys are ignored. Event payloads are produced by OOS
    /// and gain fields over time, and a handler built against an older event shape must not fail because
    /// of a field it does not use. Used by <see cref="TryReadEvent{TEvent}"/>.
    /// </remarks>
    public static JsonSerializerOptions EventOptions { get; } = CreateEventOptions();

    /// <summary>
    /// An empty JSON object, used for settings that a rule omits.
    /// </summary>
    internal static JsonElement EmptyObject { get; } = CreateEmptyObject();

    /// <summary>
    /// Reads the rule's settings from the context's input into <typeparamref name="TSettings"/> with
    /// <see cref="SettingsOptions"/>, then runs <see cref="IValidatableSettings.Validate"/> if the record
    /// implements it. Never throws for bad input.
    /// </summary>
    /// <remarks>
    /// A missing <c>settings</c> key reads as an empty object, so a record whose properties are all
    /// optional reads fine and one with required properties fails. An unknown key fails the read; unlike
    /// rule validation, only the first unknown key is named.
    /// </remarks>
    /// <typeparam name="TSettings">The action's settings record.</typeparam>
    /// <param name="context">The action context.</param>
    /// <param name="settings">The settings, when they could be read and are valid.</param>
    /// <param name="error">Why the settings could not be read or are invalid, when they could not or are not.</param>
    /// <returns><see langword="true"/> if <paramref name="settings"/> was read and is valid.</returns>
    public static bool TryReadSettings<TSettings>(RuleActionContext context,
        [NotNullWhen(true)] out TSettings? settings, [NotNullWhen(false)] out string? error)
        where TSettings : class
    {
        ArgumentNullException.ThrowIfNull(context);
        settings = null;

        if (!TryGetInput(context, out var input, out error))
            return false;

        var json = EmptyObject;
        if (input.TryGetProperty(SettingsKey, out var present))
        {
            if (present.ValueKind != JsonValueKind.Object)
            {
                error = $"Settings for action \"{context.ActionKey}\" must be a JSON object, but were {RuleJson.Describe(present.ValueKind)}.";
                return false;
            }
            json = present;
        }

        if (!TryDeserialize(json, typeof(TSettings), SettingsOptions, out var value, out var reason))
        {
            error = $"Settings for action \"{context.ActionKey}\" could not be read as {typeof(TSettings).Name}: {reason}";
            return false;
        }

        var problems = Validate(value);
        if (problems.Count > 0)
        {
            error = $"Settings for action \"{context.ActionKey}\" are invalid: {string.Join("; ", problems)}";
            return false;
        }

        settings = (TSettings)value;
        error = null;
        return true;
    }

    /// <summary>
    /// Reads the event payload from the context's input into <typeparamref name="TEvent"/> with
    /// <see cref="EventOptions"/>, so fields <typeparamref name="TEvent"/> does not declare are ignored.
    /// </summary>
    /// <remarks>
    /// Unlike settings, a missing <c>event</c> key is an error: every action is produced by an event.
    /// </remarks>
    /// <typeparam name="TEvent">The type the handler reads the event as.</typeparam>
    /// <param name="context">The action context.</param>
    /// <param name="value">The event, when it could be read.</param>
    /// <param name="error">Why the event could not be read, when it could not.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> was read.</returns>
    public static bool TryReadEvent<TEvent>(RuleActionContext context,
        [NotNullWhen(true)] out TEvent? value, [NotNullWhen(false)] out string? error)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(context);
        value = null;

        if (!TryGetInput(context, out var input, out error))
            return false;

        if (!input.TryGetProperty(EventKey, out var json))
        {
            error = $"The input for action \"{context.ActionKey}\" has no \"{EventKey}\".";
            return false;
        }

        if (json.ValueKind != JsonValueKind.Object)
        {
            error = $"The event for action \"{context.ActionKey}\" must be a JSON object, but was {RuleJson.Describe(json.ValueKind)}.";
            return false;
        }

        if (!TryDeserialize(json, typeof(TEvent), EventOptions, out var read, out var reason))
        {
            error = $"The event for action \"{context.ActionKey}\" could not be read as {typeof(TEvent).Name}: {reason}";
            return false;
        }

        value = (TEvent)read;
        error = null;
        return true;
    }

    /// <summary>
    /// Deserializes <paramref name="json"/> into <paramref name="type"/> with <paramref name="options"/>.
    /// Never throws for bad input or a broken nested type.
    /// </summary>
    /// <remarks>
    /// Besides <see cref="JsonException"/> and <see cref="NotSupportedException"/>, an
    /// <see cref="InvalidOperationException"/> is caught and reported as a failure: the startup check on
    /// <see cref="RuleActionAttribute.SettingsType"/> only covers the top-level type, so a broken nested
    /// type surfaces here, and validation must report it rather than throw.
    /// </remarks>
    /// <param name="json">The JSON to read.</param>
    /// <param name="type">The type to read it as.</param>
    /// <param name="options"><see cref="SettingsOptions"/> or <see cref="EventOptions"/>.</param>
    /// <param name="value">The value, when it could be read.</param>
    /// <param name="error">Why it could not be read, when it could not.</param>
    internal static bool TryDeserialize(JsonElement json, Type type, JsonSerializerOptions options,
        [NotNullWhen(true)] out object? value, [NotNullWhen(false)] out string? error)
    {
        try
        {
            value = json.Deserialize(type, options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            value = null;
            error = ex.Message;
            return false;
        }

        if (value is null)
        {
            error = "The value was null.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Runs <see cref="IValidatableSettings.Validate"/> when <paramref name="settings"/> implements it.
    /// </summary>
    /// <returns>Every problem found. Empty when the settings are valid or have no extra checks.</returns>
    internal static IReadOnlyList<SettingsProblem> Validate(object settings) =>
        settings is IValidatableSettings validatable ? validatable.Validate() : [];

    private static bool TryGetInput(RuleActionContext context, out JsonElement input, [NotNullWhen(false)] out string? error)
    {
        input = context.Input;
        if (input.ValueKind == JsonValueKind.Object)
        {
            error = null;
            return true;
        }

        error = $"The input for action \"{context.ActionKey}\" must be a JSON object with \"{EventKey}\" and \"{SettingsKey}\", but was {RuleJson.Describe(input.ValueKind)}.";
        return false;
    }

    private static JsonSerializerOptions CreateEventOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private static JsonSerializerOptions CreateSettingsOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            NumberHandling = JsonNumberHandling.Strict,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private static JsonElement CreateEmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
