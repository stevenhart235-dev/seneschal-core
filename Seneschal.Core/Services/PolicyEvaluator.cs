using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    public DecisionResult Evaluate(
        DecisionRequest request,
        IEnumerable<Policy> policies,
        EnforcementMode mode)
    {foreach (var policy in policies.OrderByDescending(policy => policy.Priority))
        
        {
            var evaluation = EvaluateConditions(request, policy);

            if (!evaluation.Matched)
            {
                continue;
            }

            return new DecisionResult
            {
                DecisionId = Guid.NewGuid().ToString("N"),
                RequestId = request.RequestId,
                Timestamp = DateTimeOffset.UtcNow,
                Decision = policy.Effect,
                Mode = mode,
                Reason = policy.Reason,
                MatchedPolicies = [policy.Id],
                Obligations = policy.Obligations,
                Evaluation = evaluation.Steps,
                LatencyMs = 0
            };
        }

        return new DecisionResult
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            RequestId = request.RequestId,
            Timestamp = DateTimeOffset.UtcNow,
            Decision = DecisionType.Allow,
            Mode = mode,
            Reason = "No matching policy found.",
            MatchedPolicies = [],
            Obligations = ["audit"],
            Evaluation = [],
            LatencyMs = 0
        };
    }

    private static (
        bool Matched,
        List<EvaluationStep> Steps)
        EvaluateConditions(DecisionRequest request, Policy policy)
    {
        var steps = new List<EvaluationStep>();

        foreach (var condition in policy.Conditions)
        {
            var actualValue = GetActualValue(request, condition.Key);
            var expectedValue = condition.Value;

            var matched = string.Equals(
                actualValue,
                expectedValue,
                StringComparison.OrdinalIgnoreCase);

            steps.Add(new EvaluationStep
            {
                Property = condition.Key,
                Expected = expectedValue,
                Actual = actualValue ?? "<null>",
                Matched = matched
            });
        }

        return (steps.All(step => step.Matched), steps);
    }

    private static string? GetActualValue(
        DecisionRequest request,
        string property)
    {
        return property switch
        {
            "identity.id" => request.Identity.Id,
            "identity.type" => request.Identity.Type.ToString(),
            "identity.owner" => request.Identity.Owner,
            "identity.environment" => request.Identity.Environment,
            "capability.id" => request.Capability.Id,
            "capability.provider" => request.Capability.Provider,
            "capability.category" => request.Capability.Category,
            "capability.risk" => request.Capability.Risk.ToString(),
            "intent.action" => request.Intent.Action,
            "resource.type" => request.Resource.Type,
            "resource.id" => request.Resource.Id,
            "resource.environment" => request.Resource.Environment,
            _ => null
        };
    }
}