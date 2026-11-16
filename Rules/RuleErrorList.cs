using System.Collections;

namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// An ordered list of <see cref="RuleError"/>s that parsers and validators add to.
/// </summary>
/// <remarks>
/// <para>
/// Validation reports every problem it finds, never just the first. A caller creates one list and passes it
/// to every parser and check that looks at the rule, such as <see cref="ConditionParser.TryParse(System.Text.Json.JsonElement, string, RuleErrorList)"/>
/// and any rule-system-specific parser built on top of it, so all of their errors end up together in the
/// order they were found.
/// </para>
/// <para>
/// Parsers decide whether their own part parsed cleanly by comparing <see cref="Count"/> before and after,
/// so errors already in the list never make a later part look invalid.
/// </para>
/// </remarks>
public sealed class RuleErrorList : IReadOnlyList<RuleError>
{
    private readonly List<RuleError> _errors = [];

    /// <summary>
    /// The number of errors added so far.
    /// </summary>
    public int Count => _errors.Count;

    /// <summary>
    /// The error at <paramref name="index"/>, in the order errors were added.
    /// </summary>
    public RuleError this[int index] => _errors[index];

    /// <summary>
    /// Adds an error.
    /// </summary>
    /// <param name="path">Where the problem is. See <see cref="RuleError.Path"/>.</param>
    /// <param name="message">What is wrong.</param>
    public void Add(string path, string message)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(message);
        _errors.Add(new RuleError(path, message));
    }

    /// <inheritdoc />
    public IEnumerator<RuleError> GetEnumerator() => _errors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Every error on its own line, as <c>path: message</c>.
    /// </summary>
    public override string ToString() => string.Join(Environment.NewLine, _errors);
}
