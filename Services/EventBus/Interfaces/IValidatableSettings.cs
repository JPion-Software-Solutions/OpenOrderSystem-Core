namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// Implemented by an action's settings record when it needs checks that strict deserialization can't
/// express, such as a string being blank, a GUID being empty, or a list being empty.
/// </summary>
/// <remarks>
/// <para>
/// Called in two places, so a rule that would fail at runtime fails when it is saved instead:
/// rule validation (through <see cref="EventRuleParser"/>) after deserializing a rule's settings, and
/// <see cref="RuleActionSettings.TryReadSettings{TSettings}"/> when the handler reads them.
/// </para>
/// <para>
/// Only check the settings themselves. Never look anything up or depend on the event; this runs when no
/// event exists yet.
/// </para>
/// </remarks>
public interface IValidatableSettings
{
    /// <summary>
    /// Checks the settings and returns every problem found.
    /// </summary>
    /// <returns>Every problem found. An empty list means the settings are valid.</returns>
    IReadOnlyList<SettingsProblem> Validate();
}

/// <summary>
/// One problem with an action's settings, returned by <see cref="IValidatableSettings.Validate"/>.
/// </summary>
/// <param name="Property">
/// The JSON property name the problem is with, in camelCase as written in the rule, such as
/// <c>templateName</c>. <see langword="null"/> for a problem with the settings as a whole.
/// </param>
/// <param name="Message">What is wrong, written for the person editing the rule.</param>
public sealed record SettingsProblem(string? Property, string Message)
{
    /// <summary>
    /// The problem as <c>property: message</c>, or just the message when <see cref="Property"/> is
    /// <see langword="null"/>.
    /// </summary>
    public override string ToString() => Property is null ? Message : $"{Property}: {Message}";
}
