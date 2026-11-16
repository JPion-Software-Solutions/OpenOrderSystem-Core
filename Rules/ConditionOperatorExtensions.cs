using System.Diagnostics.CodeAnalysis;

namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// Converts between <see cref="ConditionOperator"/> values and the tokens rules write in JSON.
/// </summary>
public static class ConditionOperatorExtensions
{
    private static readonly (string Token, ConditionOperator Operator)[] Tokens =
    [
        ("=", ConditionOperator.Equal),
        ("not", ConditionOperator.NotEqual),
        ("<", ConditionOperator.LessThan),
        (">", ConditionOperator.GreaterThan),
        ("<=", ConditionOperator.LessThanOrEqual),
        (">=", ConditionOperator.GreaterThanOrEqual),
        ("in", ConditionOperator.In),
        ("not in", ConditionOperator.NotIn),
        ("cont", ConditionOperator.Contains),
        ("not cont", ConditionOperator.NotContains),
        ("exists", ConditionOperator.Exists),
        ("not exists", ConditionOperator.NotExists),
    ];

    /// <summary>
    /// Every operator token, in documentation order.
    /// </summary>
    public static IReadOnlyList<string> AllTokens { get; } = Tokens.Select(t => t.Token).ToArray();

    /// <summary>
    /// The JSON token for the operator, such as <c>not in</c>.
    /// </summary>
    public static string ToToken(this ConditionOperator op)
    {
        foreach (var (token, candidate) in Tokens)
            if (candidate == op) return token;
        throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown condition operator.");
    }

    /// <summary>
    /// Looks up an operator by its JSON token. Matched exactly, case-sensitive.
    /// </summary>
    /// <param name="token">The token, such as <c>&gt;=</c>.</param>
    /// <param name="op">The operator, when the token is known.</param>
    /// <returns><see langword="true"/> if <paramref name="token"/> names an operator.</returns>
    public static bool TryParseToken(string token, [NotNullWhen(true)] out ConditionOperator? op)
    {
        foreach (var (candidateToken, candidate) in Tokens)
        {
            if (string.Equals(candidateToken, token, StringComparison.Ordinal))
            {
                op = candidate;
                return true;
            }
        }

        op = null;
        return false;
    }
}
