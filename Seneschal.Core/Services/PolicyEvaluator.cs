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
        var matches = new List<PolicyMatch>();

        foreach (var policy in policies)
        {
            var evaluation = EvaluateConditions(request, policy);

            if (!evaluation.Matched)
            {
                continue;
            }

            matches.Add(new PolicyMatch
            {
                PolicyId = policy.Id,
                PolicyName = policy.Name,
                Priority = policy.Priority,
                Effect = policy.Effect,
                Reason = policy.Reason,
                Obligations = policy.Obligations,
                Evaluation = evaluation.Steps
            });
        }

        if (matches.Count == 0)
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
                MatchedPolicyDetails = [],
                Obligations = ["audit"],
                Evaluation = [],
                LatencyMs = 0
            };
        }

        var winningMatch = ResolveWinningPolicy(matches);

        return new DecisionResult
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            RequestId = request.RequestId,
            Timestamp = DateTimeOffset.UtcNow,
            Decision = winningMatch.Effect,
            Mode = mode,
            Reason = winningMatch.Reason,
            MatchedPolicies = matches
                .OrderByDescending(match => match.Priority)
                .Select(match => match.PolicyId)
                .ToList(),
            MatchedPolicyDetails = matches
                .OrderByDescending(match => match.Priority)
                .ToList(),
            Obligations = matches
                .SelectMany(match => match.Obligations)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Evaluation = winningMatch.Evaluation,
            LatencyMs = 0
        };
    }

    private static PolicyMatch ResolveWinningPolicy(IEnumerable<PolicyMatch> matches)
    {
        return matches
            .OrderByDescending(match => match.Priority)
            .ThenByDescending(match => GetDecisionSeverity(match.Effect))
            .First();
    }

    private static int GetDecisionSeverity(DecisionType decision)
    {
        return decision switch
        {
            DecisionType.Deny => 5,
            DecisionType.RequireApproval => 4,
            DecisionType.Warn => 3,
            DecisionType.Allow => 2,
            DecisionType.LogOnly => 1,
            _ => 0
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