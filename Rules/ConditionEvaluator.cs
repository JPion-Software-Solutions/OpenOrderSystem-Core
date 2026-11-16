using System.Diagnostics;
using System.Text.Json;

namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// Evaluates a parsed condition tree against a JSON context, deciding whether it matches.
/// </summary>
/// <remarks>
/// <para>
/// A general-purpose engine: the context is whatever JSON the rule system evaluates against, such as an
/// event's payload. Fields are read from it by dotted path, so <c>payment.type</c> reads the
/// <c>type</c> property of the <c>payment</c> object. Evaluation never throws for any context; a context
/// that is not an object simply has no fields.
/// </para>
/// <para><b>Groups.</b> An empty <c>all</c> group matches; an empty <c>any</c> group does not.</para>
/// <para>
/// <b>Missing fields.</b> A field that is missing, or present but <c>null</c>, fails every condition
/// except <c>not exists</c>. <c>exists</c> matches a present, non-<c>null</c> field of any kind.
/// </para>
/// <para>
/// <b>Kinds.</b> A value's kind is number, string or boolean. JSON <c>true</c> and <c>false</c> are the
/// same kind, boolean. Objects, arrays and <c>null</c> have none of these kinds. Numbers compare by
/// numeric value, so <c>5</c> equals <c>5.0</c>. Strings compare case-insensitively (ordinal).
/// </para>
/// <para>
/// <b>Comparability.</b> Every condition is first checked for whether its field and value can be
/// compared at all. When they cannot, the condition is <see langword="false"/> for both the positive and
/// the negated operator: <c>=</c> and <c>not</c>, <c>in</c> and <c>not in</c>, <c>cont</c> and
/// <c>not cont</c> are all false. This matches the missing-field rule, so a field of the wrong kind never
/// passes a negated check by accident.
/// </para>
/// <list type="bullet">
/// <item>
/// <c>=</c>, <c>not</c>: comparable when the field and value are the same kind. A number field checked
/// with <c>= "5"</c> or <c>not "5"</c> is false either way.
/// </item>
/// <item>
/// <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c>, <c>&gt;=</c>: comparable when the field and value are the same
/// kind. Only numbers have an order, and the parser only accepts number values, so in practice the field
/// must be a number.
/// </item>
/// <item>
/// <c>in</c>, <c>not in</c>: comparable when at least one list item is the same kind as the field. Items
/// of other kinds are ignored and never match. <c>in</c> matches when a same-kind item equals the field;
/// <c>not in</c> matches when none does.
/// </item>
/// <item>
/// <c>cont</c>, <c>not cont</c> on a string field: comparable when the value is a string. <c>cont</c>
/// matches when the field contains the value, case-insensitively.
/// </item>
/// <item>
/// <c>cont</c>, <c>not cont</c> on an array field: an empty array is always comparable, so <c>cont</c>
/// is false and <c>not cont</c> is true. A non-empty array is comparable when at least one item is the same
/// kind as the value; items of other kinds never count as a match.
/// </item>
/// <item><c>cont</c>, <c>not cont</c> on any other field kind: not comparable.</item>
/// </list>
/// </remarks>
public static class ConditionEvaluator
{
    /// <summary>
    /// Returns whether <paramref name="node"/> matches <paramref name="context"/>. See the class remarks for
    /// exactly how each operator behaves.
    /// </summary>
    /// <param name="node">A tree parsed by <see cref="ConditionParser"/>.</param>
    /// <param name="context">The JSON the tree's fields are read from, usually an object.</param>
    public static bool Evaluate(ConditionNode node, JsonElement context)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            ConditionGroup { RequireAll: true } group => group.Children.All(child => Evaluate(child, context)),
            ConditionGroup group => group.Children.Any(child => Evaluate(child, context)),
            Condition condition => EvaluateCondition(condition, context),
            _ => throw new UnreachableException($"Unknown condition node type {node.GetType().FullName}."),
        };
    }

    private static bool EvaluateCondition(Condition condition, JsonElement context)
    {
        var found = TryGetField(context, condition.Field, out var field);

        switch (condition.Operator)
        {
            case ConditionOperator.Exists: return found;
            case ConditionOperator.NotExists: return !found;
        }

        if (!found) return false;

        var value = condition.Value;

        // null means "not comparable", which is false for both the positive and negated operator.
        bool? matched = condition.Operator switch
        {
            ConditionOperator.Equal or ConditionOperator.NotEqual => EqualSameKind(field, value),
            ConditionOperator.In or ConditionOperator.NotIn => InList(field, value),
            ConditionOperator.Contains or ConditionOperator.NotContains => ContainsValue(field, value),
            ConditionOperator.LessThan => CompareSameKind(field, value) is { } c ? c < 0 : null,
            ConditionOperator.GreaterThan => CompareSameKind(field, value) is { } c ? c > 0 : null,
            ConditionOperator.LessThanOrEqual => CompareSameKind(field, value) is { } c ? c <= 0 : null,
            ConditionOperator.GreaterThanOrEqual => CompareSameKind(field, value) is { } c ? c >= 0 : null,
            _ => null,
        };

        var negated = condition.Operator is ConditionOperator.NotEqual or ConditionOperator.NotIn
            or ConditionOperator.NotContains;

        return negated ? matched == false : matched == true;
    }

    /// <summary>
    /// Reads a dotted path such as <c>payment.type</c>. A <c>null</c> value counts as missing.
    /// </summary>
    private static bool TryGetField(JsonElement context, string path, out JsonElement value)
    {
        value = context;
        foreach (var part in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out var next))
            {
                value = default;
                return false;
            }
            value = next;
        }

        return value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
    }

    /// <summary>A value's kind for comparability. See the class remarks.</summary>
    private enum Kind
    {
        None,
        Number,
        String,
        Boolean,
    }

    private static Kind KindOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => Kind.Number,
        JsonValueKind.String => Kind.String,
        JsonValueKind.True or JsonValueKind.False => Kind.Boolean,
        _ => Kind.None,
    };

    /// <summary>
    /// Whether two values are equal, or <see langword="null"/> if they are not the same kind.
    /// </summary>
    private static bool? EqualSameKind(JsonElement a, JsonElement b)
    {
        var kind = KindOf(a);
        if (kind == Kind.None || kind != KindOf(b)) return null;

        return kind switch
        {
            Kind.Number => CompareNumbers(a, b) is { } c ? c == 0 : null,
            Kind.String => string.Equals(a.GetString(), b.GetString(), StringComparison.OrdinalIgnoreCase),
            Kind.Boolean => a.GetBoolean() == b.GetBoolean(),
            _ => null,
        };
    }

    /// <summary>
    /// Orders two values, or returns <see langword="null"/> if they are not the same kind or that kind has
    /// no order. Only numbers have an order.
    /// </summary>
    private static int? CompareSameKind(JsonElement a, JsonElement b) =>
        KindOf(a) == Kind.Number && KindOf(b) == Kind.Number ? CompareNumbers(a, b) : null;

    /// <summary>
    /// Compares two JSON numbers exactly as decimals when both fit, otherwise as doubles. Returns
    /// <see langword="null"/> if either cannot be read as a finite number.
    /// </summary>
    private static int? CompareNumbers(JsonElement a, JsonElement b)
    {
        if (a.TryGetDecimal(out var x) && b.TryGetDecimal(out var y))
            return x.CompareTo(y);

        if (a.TryGetDouble(out var dx) && b.TryGetDouble(out var dy) && double.IsFinite(dx) && double.IsFinite(dy))
            return dx.CompareTo(dy);

        return null;
    }

    /// <summary>
    /// <c>in</c>: whether a same-kind item equals the field, or <see langword="null"/> when no item is the
    /// same kind as the field.
    /// </summary>
    private static bool? InList(JsonElement field, JsonElement list) =>
        list.ValueKind == JsonValueKind.Array ? AnySameKindMatch(list, field) : null;

    /// <summary>
    /// <c>cont</c>: substring for a string field, membership for an array field. See the class remarks.
    /// </summary>
    private static bool? ContainsValue(JsonElement field, JsonElement value) => field.ValueKind switch
    {
        JsonValueKind.String when value.ValueKind == JsonValueKind.String =>
            field.GetString()!.Contains(value.GetString()!, StringComparison.OrdinalIgnoreCase),
        JsonValueKind.Array when field.GetArrayLength() == 0 => false,
        JsonValueKind.Array => AnySameKindMatch(field, value),
        _ => null,
    };

    /// <summary>
    /// Whether any item in <paramref name="array"/> of the same kind as <paramref name="target"/> equals it.
    /// Items of other kinds are skipped. Returns <see langword="null"/> when no item is the same kind.
    /// </summary>
    private static bool? AnySameKindMatch(JsonElement array, JsonElement target)
    {
        var comparable = false;
        foreach (var item in array.EnumerateArray())
        {
            if (EqualSameKind(item, target) is not { } equal) continue;
            if (equal) return true;
            comparable = true;
        }

        return comparable ? false : null;
    }
}
