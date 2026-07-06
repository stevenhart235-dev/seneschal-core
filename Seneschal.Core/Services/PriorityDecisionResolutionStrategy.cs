using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class PriorityDecisionResolutionStrategy : IDecisionResolutionStrategy
{
    public PolicyMatch SelectWinner(IReadOnlyCollection<PolicyMatch> matches)
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
