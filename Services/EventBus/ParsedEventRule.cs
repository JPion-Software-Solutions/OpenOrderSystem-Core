using OpenOrderSystem.Core.Rules;

namespace OpenOrderSystem.Core.Services.EventBus;

/// <summary>
/// The parts of an event rule body that <see cref="EventRuleParser"/> parsed cleanly.
/// </summary>
/// <remarks>
/// From <see cref="EventRuleParser.TryParse(Interfaces.IRuleActionCatalog, int, string, RuleErrorList)"/>
/// this may be partial: the trigger can be <see langword="null"/> while some actions parsed, and
/// <see cref="Actions"/> only holds the entries that had no problems. From
/// <see cref="EventRuleParser.Parse(Interfaces.IRuleActionCatalog, int, string)"/> it is always complete.
/// </remarks>
/// <param name="Trigger">The parsed trigger, or <see langword="null"/> if it was missing or had any errors.</param>
/// <param name="Actions">Every action entry whose key, target and settings were valid, in body order.</param>
public sealed record ParsedEventRule(ConditionNode? Trigger, IReadOnlyList<ParsedRuleAction> Actions)
{
    /// <summary>
    /// A result with nothing parsed, for a body that could not be read at all.
    /// </summary>
    public static ParsedEventRule Empty { get; } = new(null, []);
}
