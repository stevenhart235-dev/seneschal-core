using ApiDecisionResult = Seneschal.Api.Models.DecisionResult;
using CoreDecisionResult = Seneschal.Core.Models.DecisionResult;
using CoreEnforcementMode = Seneschal.Core.Enums.EnforcementMode;
using Seneschal.Core.Enums;

namespace Seneschal.Api.Mappers;

public static class DecisionResultMapper
{
    public static ApiDecisionResult ToApi(
        CoreDecisionResult result,
        long durationMs,
        string? governanceWindowName = null,
        string? governanceWindowMode = null,
        string? governanceWindowReason = null,
        bool governanceWindowInfluencedResult = false,
        bool includeSimulationExplanation = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        var decision = DecisionTypeMapper.ToApi(result.Decision);

        return new ApiDecisionResult
        {
            Decision = decision,
            Reason = result.Reason,
            PolicyMatched = result.WinningPolicy?.PolicyName ?? string.Empty,
            DurationMs = durationMs,
            EffectiveAction =
                result.Mode == CoreEnforcementMode.LogOnly &&
                result.Decision != DecisionType.Allow
                    ? "logged_only"
                    : decision,
            Mode = EnforcementModeMapper.ToApi(result.Mode)
            ,ExecutionGuidance = result.ExecutionGuidance.ToString()
            ,ApprovalId = result.ApprovalId
            ,ApprovalStatus = result.ApprovalStatus
            ,OperationId = result.OperationId
            ,ApprovalCorrelationMode = result.ApprovalCorrelationMode
            ,Message = result.CallerMessage
            ,RetryGuidance = result.RetryGuidance
            ,MatchedPolicies = includeSimulationExplanation
                ? result.MatchedPolicies.ToList()
                : null
            ,GovernanceWindowName = includeSimulationExplanation
                ? governanceWindowName
                : null
            ,GovernanceWindowMode = includeSimulationExplanation
                ? governanceWindowMode
                : null
            ,GovernanceWindowReason = includeSimulationExplanation
                ? governanceWindowReason
                : null
            ,GovernanceWindowInfluencedResult = includeSimulationExplanation
                ? governanceWindowInfluencedResult
                : null
        };
    }
}
