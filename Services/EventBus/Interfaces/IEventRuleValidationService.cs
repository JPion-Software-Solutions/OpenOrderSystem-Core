using OpenOrderSystem.Core.Data.DataModels.V2.EventBus;
using OpenOrderSystem.Core.Rules;

namespace OpenOrderSystem.Core.Services.EventBus.Interfaces;

/// <summary>
/// Save-time validation of a whole <see cref="EventRule"/>: its own fields, its body, and the devices
/// its actions target.
/// </summary>
/// <remarks>
/// Reports every problem in one list rather than stopping at the first, so the rule editor can show them
/// all at once. Never throws for an invalid rule.
/// </remarks>
public interface IEventRuleValidationService
{
    /// <summary>
    /// Validates <paramref name="rule"/> and returns every problem found.
    /// </summary>
    /// <param name="rule">The rule to validate.</param>
    /// <param name="ct">Cancels the device lookup.</param>
    /// <returns>
    /// Every problem found, with paths such as <c>eventKey</c>, <c>flags</c>, <c>rulesVersion</c>,
    /// <c>trigger.all[0].op</c> or <c>actions[1].target</c>. Empty when the rule is valid.
    /// </returns>
    Task<IReadOnlyList<RuleError>> ValidateAsync(EventRule rule, CancellationToken ct = default);
}
