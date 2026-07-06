using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class DecisionResolver
{
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

        var winningMatch = ResolveWinningPolicy(matchList);

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
}
