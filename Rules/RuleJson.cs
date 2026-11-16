using System.Text.Json;

namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// Small JSON helpers shared by the rule parsers.
/// </summary>
public static class RuleJson
{
    /// <summary>
    /// Reports every key of <paramref name="obj"/> that is not in <paramref name="allowed"/>, and every
    /// key that appears more than once, so typos like <c>vaule</c> fail instead of being ignored.
    /// </summary>
    /// <param name="obj">The JSON object to check. Must be an object.</param>
    /// <param name="path">The object's path. Each error is reported at the offending key's path.</param>
    /// <param name="allowed">The keys the object may have. Matched exactly, case-sensitive.</param>
    /// <param name="errors">The list errors are added to.</param>
    public static void CheckKeys(JsonElement obj, string path, IReadOnlyList<string> allowed, RuleErrorList errors)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(allowed);
        ArgumentNullException.ThrowIfNull(errors);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in obj.EnumerateObject())
        {
            var keyPath = RulePath.Property(path, property.Name);
            if (!seen.Add(property.Name))
                errors.Add(keyPath, $"The key \"{property.Name}\" appears more than once.");
            else if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                errors.Add(keyPath, $"Unknown key \"{property.Name}\". Allowed keys: {string.Join(", ", allowed.Select(k => $"\"{k}\""))}.");
        }
    }

    /// <summary>
    /// A JSON value kind as a short phrase for error messages, such as "a string" or "an array".
    /// <see cref="JsonValueKind.True"/> and <see cref="JsonValueKind.False"/> are both "a boolean".
    /// </summary>
    public static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "nothing",
    };
}
