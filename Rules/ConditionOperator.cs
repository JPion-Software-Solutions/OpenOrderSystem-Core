namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// The operators a <see cref="Condition"/> can use. Each is written in JSON as the token shown on its
/// member, such as <c>"op": "not in"</c>.
/// </summary>
/// <remarks>
/// See <see cref="ConditionEvaluator"/> for exactly when each operator matches.
/// </remarks>
public enum ConditionOperator
{
    /// <summary><c>=</c>: the field equals the value. Strings compare case-insensitively.</summary>
    Equal,

    /// <summary><c>not</c>: the field does not equal the value.</summary>
    NotEqual,

    /// <summary><c>&lt;</c>: the field is a number less than the value.</summary>
    LessThan,

    /// <summary><c>&gt;</c>: the field is a number greater than the value.</summary>
    GreaterThan,

    /// <summary><c>&lt;=</c>: the field is a number less than or equal to the value.</summary>
    LessThanOrEqual,

    /// <summary><c>&gt;=</c>: the field is a number greater than or equal to the value.</summary>
    GreaterThanOrEqual,

    /// <summary><c>in</c>: the field equals one of the items in the value's list.</summary>
    In,

    /// <summary><c>not in</c>: the field equals none of the items in the value's list.</summary>
    NotIn,

    /// <summary>
    /// <c>cont</c>: the field is a string containing the value (case-insensitive), or an array with an item
    /// equal to the value.
    /// </summary>
    Contains,

    /// <summary><c>not cont</c>: the negation of <see cref="Contains"/>.</summary>
    NotContains,

    /// <summary><c>exists</c>: the field is present and not <c>null</c>. Takes no value.</summary>
    Exists,

    /// <summary><c>not exists</c>: the field is missing or <c>null</c>. Takes no value.</summary>
    NotExists,
}
