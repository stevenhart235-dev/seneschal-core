using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class DecisionResolver
{
    private readonly IDecisionResolutionStrategy _resolutionStrategy;

    public DecisionResolver()
        : this(new PriorityDecisionResolutionStrategy())
    {
    }

    public DecisionResolver(IDecisionResolutionStrategy resolutionStrategy)
    {
        _resolutionStrategy = resolutionStrategy;
    }

    public DecisionResult Resolve(
        DecisionRequest request,
        IEnumerable<PolicyMatch> matches,
        EnforcementMode mode)
    {
        var matchList = matches.ToList();

        if (matchList.Count == 0)
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

        var winningMatch = _resolutionStrategy.SelectWinner(matchList);

        return new DecisionResult
        {
            DecisionId = Guid.NewGuid().ToString("N"),
            RequestId = request.RequestId,
            Timestamp = DateTimeOffset.UtcNow,
            Decision = winningMatch.Effect,
            Mode = mode,
            Reason = winningMatch.Reason,
            MatchedPolicies = matchList
                .OrderByDescending(match => match.Priority)
                .Select(match => match.PolicyId)
                .ToList(),
            MatchedPolicyDetails = matchList
                .OrderByDescending(match => match.Priority)
                .ToList(),
            Obligations = matchList
                .SelectMany(match => match.Obligations)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Evaluation = winningMatch.Evaluation,
            LatencyMs = 0
        };
    }
}
