using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class PolicyEvaluator : IPolicyEvaluator
{
    private readonly DecisionResolver _decisionResolver;

    public PolicyEvaluator()
        : this(new DecisionResolver())
    {
    }

    public PolicyEvaluator(DecisionResolver decisionResolver)
    {
        _decisionResolver = decisionResolver;
    }

    public DecisionResult Evaluate(
        DecisionRequest request,
        IEnumerable<Policy> policies,
        EnforcementMode mode)
    {
        var matches = new List<PolicyMatch>();
        var policyEvaluations = new List<PolicyEvaluation>();

        foreach (var policy in policies)
        {
            var evaluation = EvaluateConditions(request, policy);

            policyEvaluations.Add(new PolicyEvaluation
            {
                Policy = policy,
                Matched = evaluation.Matched,
                Reasons = evaluation.Steps
                    .Where(step => !step.Matched)
                    .Select(step => $"{step.Property} mismatch")
                    .ToList(),
                Obligations = policy.Obligations,
                Conditions = evaluation.Steps
            });

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

        return _decisionResolver.Resolve(request, matches, mode) with
        {
            PolicyEvaluations = policyEvaluations
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
