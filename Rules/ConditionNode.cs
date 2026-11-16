namespace OpenOrderSystem.Core.Rules;

/// <summary>
/// A node in a parsed condition tree: either a <see cref="ConditionGroup"/> or a <see cref="Condition"/>.
/// </summary>
/// <remarks>
/// Produced by <see cref="ConditionParser"/> and read by <see cref="ConditionEvaluator"/>. A tree is
/// immutable, so parse it once (for example when a rule is saved or loaded) and evaluate it as many times
/// as needed. The only node types are the two in this namespace.
/// </remarks>
public abstract record ConditionNode
{
    private protected ConditionNode()
    {
    }
}
