using System.Text.Json;

namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// One condition: compares a field from the evaluation context with a value using an operator.
/// </summary>
/// <param name="Field">
/// The field to read from the context, as a dotted path such as <c>payment.type</c>. Never blank, and no
/// segment is empty.
/// </param>
/// <param name="Operator">How the field is compared with <paramref name="Value"/>.</param>
/// <param name="Value">
/// The value to compare with. <see langword="default"/> (<see cref="JsonValueKind.Undefined"/>) for
/// <see cref="ConditionOperator.Exists"/> and <see cref="ConditionOperator.NotExists"/>. Otherwise a
/// cloned element that does not depend on the document it was parsed from.
/// </param>
public sealed record Condition(string Field, ConditionOperator Operator, JsonElement Value) : ConditionNode;
