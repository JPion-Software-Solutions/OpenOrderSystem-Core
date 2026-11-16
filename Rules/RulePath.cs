namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// Builds the error paths used in <see cref="RuleError.Path"/>, such as <c>trigger.all[1].any[0]</c>.
/// </summary>
public static class RulePath
{
    /// <summary>
    /// The path of a property of the object at <paramref name="parent"/>, such as <c>trigger.all</c>.
    /// When <paramref name="parent"/> is empty the property is at the root, so the result is just
    /// <paramref name="name"/>.
    /// </summary>
    /// <param name="parent">The path of the object holding the property. May be empty.</param>
    /// <param name="name">The property name.</param>
    public static string Property(string parent, string name) =>
        string.IsNullOrEmpty(parent) ? name : $"{parent}.{name}";

    /// <summary>
    /// The path of an item in the array at <paramref name="parent"/>, such as <c>trigger.all[1]</c>.
    /// </summary>
    /// <param name="parent">The path of the array.</param>
    /// <param name="index">The item's zero-based index.</param>
    public static string Index(string parent, int index) => $"{parent}[{index}]";
}
