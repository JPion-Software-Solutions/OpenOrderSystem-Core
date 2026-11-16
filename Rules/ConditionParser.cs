using System.Text.Json;

namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// Parses a condition tree from JSON into <see cref="ConditionNode"/>s that
/// <see cref="ConditionEvaluator"/> can evaluate.
/// </summary>
/// <remarks>
/// <para>
/// This is a general-purpose condition engine. It knows nothing about the rule system it is used by: a
/// rule system decides where its condition tree lives in its own JSON, passes that element here with a
/// root path, and adds its own checks around it.
/// </para>
/// <para>
/// A tree is either a group or a condition:
/// </para>
/// <code>
/// { "all": [ ...nodes... ] }                         // every child must match
/// { "any": [ ...nodes... ] }                         // at least one child must match
/// { "field": "payment.type", "op": "in", "value": ["cash", "check"] }
/// { "field": "customer.email", "op": "exists" }
/// </code>
/// <para>
/// The parser checks the whole tree and reports every problem it finds:
/// </para>
/// <list type="bullet">
/// <item>every node is an object; a group has exactly one of <c>all</c> or <c>any</c>, holding an array</item>
/// <item>unknown or repeated keys are rejected, so a typo like <c>vaule</c> fails</item>
/// <item><c>field</c> is a non-blank string with no empty dotted segments</item>
/// <item><c>op</c> is one of the tokens in <see cref="ConditionOperatorExtensions.AllTokens"/></item>
/// <item><c>exists</c> and <c>not exists</c> have no <c>value</c>; every other operator has one</item>
/// <item>
/// the value's JSON kind suits the operator: a number for <c>&lt;</c>, <c>&gt;</c>, <c>&lt;=</c> and
/// <c>&gt;=</c>; a string, number or boolean for <c>=</c>, <c>not</c>, <c>cont</c> and <c>not cont</c>;
/// a non-empty array of strings, numbers or booleans for <c>in</c> and <c>not in</c>
/// </item>
/// </list>
/// <para>
/// <see cref="TryParse(JsonElement, string, RuleErrorList)"/> is the non-throwing core. The
/// <c>Parse</c> overloads wrap it for callers that just want a tree or an exception.
/// </para>
/// </remarks>
public static class ConditionParser
{
    private static readonly string[] GroupKeys = ["all", "any"];
    private static readonly string[] ConditionKeys = ["field", "op", "value"];

    /// <summary>
    /// Parses a condition tree, adding every problem it finds to <paramref name="errors"/>. Never throws for
    /// bad input.
    /// </summary>
    /// <param name="element">The root node of the tree.</param>
    /// <param name="rootPath">
    /// The path of <paramref name="element"/> in the caller's document, such as <c>trigger</c>. Error paths
    /// are built from it, such as <c>trigger.all[1].any[0]</c>. May be empty.
    /// </param>
    /// <param name="errors">The list errors are added to. Errors already in it are left alone.</param>
    /// <returns>
    /// The parsed tree, or <see langword="null"/> if this call added any errors. Values in the tree are
    /// cloned, so the tree outlives the document <paramref name="element"/> came from.
    /// </returns>
    public static ConditionNode? TryParse(JsonElement element, string rootPath, RuleErrorList errors)
    {
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(errors);

        var before = errors.Count;
        var node = ParseNode(element, rootPath, errors);
        return errors.Count == before ? node : null;
    }

    /// <summary>
    /// Parses a condition tree from a JSON string, adding every problem it finds to
    /// <paramref name="errors"/>. Never throws for bad input: invalid JSON is added as a single error at
    /// <paramref name="rootPath"/>.
    /// </summary>
    /// <param name="json">The JSON text of the root node.</param>
    /// <param name="rootPath">The path error paths are built from. See <see cref="TryParse(JsonElement, string, RuleErrorList)"/>.</param>
    /// <param name="errors">The list errors are added to.</param>
    /// <returns>The parsed tree, or <see langword="null"/> if this call added any errors.</returns>
    public static ConditionNode? TryParse(string json, string rootPath, RuleErrorList errors)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(rootPath);
        ArgumentNullException.ThrowIfNull(errors);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            errors.Add(rootPath, $"Not valid JSON: {ex.Message}");
            return null;
        }

        using (document)
            return TryParse(document.RootElement, rootPath, errors);
    }

    /// <summary>
    /// Parses a condition tree, throwing if it has any problems.
    /// </summary>
    /// <param name="element">The root node of the tree.</param>
    /// <param name="rootPath">The path error paths are built from. May be empty.</param>
    /// <returns>The parsed tree.</returns>
    /// <exception cref="RuleParseException">Thrown, carrying every problem found, if the tree is invalid.</exception>
    public static ConditionNode Parse(JsonElement element, string rootPath = "")
    {
        var errors = new RuleErrorList();
        var node = TryParse(element, rootPath, errors);
        return errors.Count == 0 && node is not null ? node : throw new RuleParseException(errors);
    }

    /// <summary>
    /// Parses a condition tree from a JSON string, throwing if the JSON or the tree has any problems.
    /// </summary>
    /// <param name="json">The JSON text of the root node.</param>
    /// <param name="rootPath">The path error paths are built from. May be empty.</param>
    /// <returns>The parsed tree.</returns>
    /// <exception cref="RuleParseException">Thrown, carrying every problem found, if the JSON or tree is invalid.</exception>
    public static ConditionNode Parse(string json, string rootPath = "")
    {
        var errors = new RuleErrorList();
        var node = TryParse(json, rootPath, errors);
        return errors.Count == 0 && node is not null ? node : throw new RuleParseException(errors);
    }

    /// <summary>
    /// Parses one node and everything under it. Keeps walking after errors so every problem is reported.
    /// Returns <see langword="null"/> when the node could not be built; the caller decides validity by
    /// counting errors.
    /// </summary>
    private static ConditionNode? ParseNode(JsonElement element, string path, RuleErrorList errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(path, $"Expected an object (a group with \"all\" or \"any\", or a condition with \"field\" and \"op\"), but found {RuleJson.Describe(element.ValueKind)}.");
            return null;
        }

        var hasAll = element.TryGetProperty("all", out var all);
        var hasAny = element.TryGetProperty("any", out var any);

        return hasAll || hasAny
            ? ParseGroup(element, path, hasAll, all, hasAny, any, errors)
            : ParseCondition(element, path, errors);
    }

    private static ConditionGroup? ParseGroup(JsonElement element, string path, bool hasAll, JsonElement all,
        bool hasAny, JsonElement any, RuleErrorList errors)
    {
        RuleJson.CheckKeys(element, path, GroupKeys, errors);

        if (hasAll && hasAny)
            errors.Add(path, "A group must have \"all\" or \"any\", not both.");

        // Walk both lists even when both are present, so problems inside either are still reported.
        var allGroup = hasAll ? ParseChildren(all, RulePath.Property(path, "all"), requireAll: true, errors) : null;
        var anyGroup = hasAny ? ParseChildren(any, RulePath.Property(path, "any"), requireAll: false, errors) : null;

        return hasAll && hasAny ? null : allGroup ?? anyGroup;
    }

    private static ConditionGroup? ParseChildren(JsonElement array, string path, bool requireAll, RuleErrorList errors)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            errors.Add(path, $"Expected an array of conditions and groups, but found {RuleJson.Describe(array.ValueKind)}.");
            return null;
        }

        var children = new List<ConditionNode>(array.GetArrayLength());
        var complete = true;
        var index = 0;
        foreach (var child in array.EnumerateArray())
        {
            var node = ParseNode(child, RulePath.Index(path, index++), errors);
            if (node is null) complete = false;
            else children.Add(node);
        }

        return complete ? new ConditionGroup(requireAll, children) : null;
    }

    private static Condition? ParseCondition(JsonElement element, string path, RuleErrorList errors)
    {
        RuleJson.CheckKeys(element, path, ConditionKeys, errors);

        var field = ReadField(element, path, errors);
        var op = ReadOperator(element, path, errors);

        var valuePath = RulePath.Property(path, "value");
        var hasValue = element.TryGetProperty("value", out var value);

        // Without a known operator there is nothing to check the value against.
        if (op is null) return null;

        if (op is ConditionOperator.Exists or ConditionOperator.NotExists)
        {
            if (hasValue)
                errors.Add(valuePath, $"Operator \"{op.Value.ToToken()}\" does not take a value. Remove \"value\".");
            return field is null || hasValue ? null : new Condition(field, op.Value, default);
        }

        if (!hasValue)
        {
            errors.Add(valuePath, $"Required: operator \"{op.Value.ToToken()}\" needs a value.");
            return null;
        }

        var valueValid = CheckValue(op.Value, value, valuePath, errors);
        return field is null || !valueValid ? null : new Condition(field, op.Value, value.Clone());
    }

    private static string? ReadField(JsonElement element, string path, RuleErrorList errors)
    {
        var fieldPath = RulePath.Property(path, "field");

        if (!element.TryGetProperty("field", out var field))
        {
            errors.Add(fieldPath, "Required: a condition needs a \"field\" naming the value to test.");
            return null;
        }

        if (field.ValueKind != JsonValueKind.String)
        {
            errors.Add(fieldPath, $"Expected a string, but found {RuleJson.Describe(field.ValueKind)}.");
            return null;
        }

        var name = field.GetString()!;
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(fieldPath, "Must not be blank.");
            return null;
        }

        if (name.Split('.').Any(string.IsNullOrWhiteSpace))
        {
            errors.Add(fieldPath, $"\"{name}\" has an empty segment. Separate nested field names with single dots, such as \"payment.type\".");
            return null;
        }

        return name;
    }

    private static ConditionOperator? ReadOperator(JsonElement element, string path, RuleErrorList errors)
    {
        var opPath = RulePath.Property(path, "op");

        if (!element.TryGetProperty("op", out var op))
        {
            errors.Add(opPath, "Required: a condition needs an \"op\".");
            return null;
        }

        if (op.ValueKind != JsonValueKind.String)
        {
            errors.Add(opPath, $"Expected a string, but found {RuleJson.Describe(op.ValueKind)}.");
            return null;
        }

        var token = op.GetString()!;
        if (ConditionOperatorExtensions.TryParseToken(token, out var parsed))
            return parsed;

        errors.Add(opPath, $"Unknown operator \"{token}\". Use one of: {string.Join(", ", ConditionOperatorExtensions.AllTokens.Select(t => $"\"{t}\""))}.");
        return null;
    }

    /// <summary>
    /// Checks that the value's JSON kind suits the operator. Returns <see langword="true"/> if it does.
    /// </summary>
    private static bool CheckValue(ConditionOperator op, JsonElement value, string path, RuleErrorList errors)
    {
        var token = op.ToToken();

        switch (op)
        {
            case ConditionOperator.LessThan or ConditionOperator.GreaterThan
                or ConditionOperator.LessThanOrEqual or ConditionOperator.GreaterThanOrEqual:
                if (value.ValueKind == JsonValueKind.Number) return true;
                errors.Add(path, $"Operator \"{token}\" needs a number, but found {RuleJson.Describe(value.ValueKind)}.");
                return false;

            case ConditionOperator.In or ConditionOperator.NotIn:
                if (value.ValueKind != JsonValueKind.Array)
                {
                    errors.Add(path, $"Operator \"{token}\" needs a non-empty array of strings, numbers or booleans, but found {RuleJson.Describe(value.ValueKind)}.");
                    return false;
                }

                if (value.GetArrayLength() == 0)
                {
                    errors.Add(path, $"Operator \"{token}\" needs at least one item to compare with.");
                    return false;
                }

                var valid = true;
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (!IsScalar(item))
                    {
                        errors.Add(RulePath.Index(path, index), $"List items must be strings, numbers or booleans, but found {RuleJson.Describe(item.ValueKind)}.");
                        valid = false;
                    }
                    index++;
                }
                return valid;

            default:
                // =, not, cont, not cont
                if (IsScalar(value)) return true;
                errors.Add(path, $"Operator \"{token}\" needs a string, number or boolean, but found {RuleJson.Describe(value.ValueKind)}.");
                return false;
        }
    }

    private static bool IsScalar(JsonElement element) =>
        element.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
}
