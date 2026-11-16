namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// One problem found while parsing or validating a rule.
/// </summary>
/// <param name="Path">
/// Where the problem is, such as <c>trigger.all[1].any[0].value</c>. Built from the root path the caller
/// supplied, so it reads the same way the rule's JSON is laid out. Empty when the problem is with the
/// whole document, such as invalid JSON in a rule body whose root path is empty.
/// </param>
/// <param name="Message">What is wrong, written for the person editing the rule.</param>
public sealed record RuleError(string Path, string Message)
{
    /// <summary>
    /// The error as <c>path: message</c>, or just the message when <see cref="Path"/> is empty.
    /// </summary>
    public override string ToString() => string.IsNullOrEmpty(Path) ? Message : $"{Path}: {Message}";
}
