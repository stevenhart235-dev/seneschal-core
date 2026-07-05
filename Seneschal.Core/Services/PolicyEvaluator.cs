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
    {
        var matchedPolicy = FindMatchingPolicy(request, policies);

        if (matchedPolicy is null)
        {
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
                LatencyMs = 0
            };
        }

        return new DecisionResult
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            RequestId = request.RequestId,
            Timestamp = DateTimeOffset.UtcNow,
            Decision = matchedPolicy.Effect,
            Mode = mode,
            Reason = matchedPolicy.Reason,
            MatchedPolicies = [matchedPolicy.Id],
            Obligations = matchedPolicy.Obligations,
            LatencyMs = 0
        };
    }

    private static Policy? FindMatchingPolicy(
        DecisionRequest request,
        IEnumerable<Policy> policies)
    {
        foreach (var policy in policies)
        {
            if (Matches(request, policy))
            {
                return policy;
            }
        }

        return null;
    }

    private static bool Matches(DecisionRequest request, Policy policy)
    {
        foreach (var condition in policy.Conditions)
        {
            var actualValue = condition.Key switch
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

            if (!string.Equals(actualValue, condition.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}