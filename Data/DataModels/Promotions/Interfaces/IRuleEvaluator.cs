using OpenOrderSystem.Core.Data.DataModels.Ordering.Entities;

namespace OpenOrderSystem.Core.Data.DataModels.Promotions.Interfaces
{
    public interface IRuleEvaluator
    {
        public Task<RuleEvaluatorResult> Evaluate(RuleEvaluatorContext context);
    }

    public class RuleEvaluatorResult
    {
        public static RuleEvaluatorResult Ok => new RuleEvaluatorResult
        {
            Success = true,
        };

        public static RuleEvaluatorResult Error(string msg) => new RuleEvaluatorResult
        {
            Success = false,
            ErrorMsg = msg
        };

        public bool Success { get; init; }

        public string ErrorMsg { get; init; } = string.Empty;

        public string? MissingContext { get; init; }

        public bool NeedsAdditionalContext => MissingContext != null;
    }

    public class RuleEvaluatorContext
    {
        public Order? Order { get; init; }
        public HttpContext? HttpContext { get; init; }

        public Dictionary<string, object> AdditionalContext { get; private set; } = new Dictionary<string, object>();
    }
}
