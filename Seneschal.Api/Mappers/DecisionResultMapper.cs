using ApiDecisionResult = Seneschal.Api.Models.DecisionResult;
using CoreDecisionResult = Seneschal.Core.Models.DecisionResult;
using CoreEnforcementMode = Seneschal.Core.Enums.EnforcementMode;
using Seneschal.Core.Enums;

namespace Seneschal.Api.Mappers;

public static class DecisionResultMapper
{
    public static ApiDecisionResult ToApi(
        CoreDecisionResult result,
        long durationMs)
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
        };
    }
}
