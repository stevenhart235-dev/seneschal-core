using ApiAuditEvent = Seneschal.Api.Models.AuditEvent;
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
            GovernanceWindowMessage = auditEvent.GovernanceWindowMessage
        };
    }
}
