using ApiAuditEvent = Seneschal.Api.Models.AuditEvent;
using AuditConditionEvaluation = Seneschal.Api.Models.AuditConditionEvaluation;
using AuditPolicyEvaluation = Seneschal.Api.Models.AuditPolicyEvaluation;
using CoreAuditEvent = Seneschal.Core.Models.AuditEvent;

namespace Seneschal.Api.Mappers;

public static class AuditEventMapper
{
    public static ApiAuditEvent ToApi(CoreAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return new ApiAuditEvent
        {
            Id = auditEvent.Id,
            RequestId = auditEvent.RequestId,
            TimestampUtc = auditEvent.TimestampUtc,
            IdentityId = auditEvent.IdentityId,
            CapabilityId = auditEvent.CapabilityId,
            ResourceId = auditEvent.ResourceId,
            Environment = auditEvent.Environment,
            Decision = DecisionTypeMapper.ToApi(auditEvent.Decision),
            EnforcementMode = EnforcementModeMapper.ToApi(
                auditEvent.EnforcementMode),
            MatchedPolicies = auditEvent.MatchedPolicies,
            Obligations = auditEvent.Obligations,
            Reason = auditEvent.Reason,
            EvaluationDurationMs = auditEvent.EvaluationDurationMs,
            GovernanceWindowName = auditEvent.GovernanceWindowName,
            GovernanceWindowMode = auditEvent.GovernanceWindowMode,
            GovernanceWindowMessage = auditEvent.GovernanceWindowMessage,
            GovernanceWindowReason = auditEvent.GovernanceWindowReason,
            PolicyDecision = DecisionTypeMapper.ToApi(auditEvent.PolicyDecision),
            PolicyReason = auditEvent.PolicyReason,
            ApprovalId = auditEvent.ApprovalId,
            ApprovalStatus = auditEvent.ApprovalStatus,
            ApprovalAction = auditEvent.ApprovalAction,
            ApprovalRequestReason = auditEvent.ApprovalRequestReason,
            ApprovalResolvedAt = auditEvent.ApprovalResolvedAt,
            ApprovalResolvedBy = auditEvent.ApprovalResolvedBy,
            PolicyEvaluations = auditEvent.PolicyEvaluations.Select(policy =>
                new AuditPolicyEvaluation
                {
                    PolicyId = policy.Policy.Id,
                    PolicyName = policy.Policy.Name,
                    Matched = policy.Matched,
                    Conditions = policy.Conditions.Select(condition =>
                        new AuditConditionEvaluation
                        {
                            Condition = condition.Property,
                            Expected = condition.Expected,
                            Actual = condition.Actual,
                            Passed = condition.Matched
                        }).ToList()
                }).ToList()
        };
    }
}
