namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// Thrown by the throwing parse wrappers, such as <see cref="ConditionParser.Parse(System.Text.Json.JsonElement, string)"/>,
/// when the input has one or more problems. Carries every problem found, not just the first.
/// </summary>
/// <remarks>
/// This is the only exception the rule parsers throw for bad input. Callers that want to collect errors
/// without exceptions should call the <c>TryParse</c> cores with their own <see cref="RuleErrorList"/>
/// instead; the parsers never use exceptions to pass errors between each other.
/// </remarks>
public sealed class RuleParseException : Exception
{
    /// <summary>
    /// Creates the exception from the errors a parse found.
    /// </summary>
    /// <param name="errors">Every error found. Must not be empty.</param>
    public RuleParseException(IEnumerable<RuleError> errors)
        : this(Snapshot(errors))
    {
    }

    private RuleParseException(RuleError[] errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>
    /// Every error the parse found, in the order they were found.
    /// </summary>
    public IReadOnlyList<RuleError> Errors { get; }

    private static RuleError[] Snapshot(IEnumerable<RuleError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var snapshot = errors.ToArray();
        if (snapshot.Length == 0)
            throw new ArgumentException("A rule parse exception needs at least one error.", nameof(errors));
        return snapshot;
    }

    private static string BuildMessage(RuleError[] errors)
    {
        var heading = errors.Length == 1 ? "The rule has 1 error:" : $"The rule has {errors.Length} errors:";
        return heading + string.Concat(errors.Select(e => $"{Environment.NewLine}  - {e}"));
    }
}
