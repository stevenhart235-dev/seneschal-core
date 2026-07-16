namespace Seneschal.Api.Models;

public class AuditEvent
{
    public string Id { get; set; } = "";
    public string RequestId { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string IdentityId { get; set; } = "";
    public string CapabilityId { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Decision { get; set; } = "";
    public string EnforcementMode { get; set; } = "";
    public List<string> MatchedPolicies { get; set; } = new();
    public List<string> Obligations { get; set; } = new();
    public string Reason { get; set; } = "";
    public long EvaluationDurationMs { get; set; }
    public string? GovernanceWindowName { get; set; }
    public string? GovernanceWindowMode { get; set; }
    public string? GovernanceWindowMessage { get; set; }
    public string? GovernanceWindowReason { get; set; }
    public string PolicyDecision { get; set; } = "";
    public string PolicyReason { get; set; } = "";
    public List<AuditPolicyEvaluation> PolicyEvaluations { get; set; } = new();
    public string? ApprovalId { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalAction { get; set; }
    public string? ApprovalRequestReason { get; set; }
    public DateTimeOffset? ApprovalResolvedAt { get; set; }
    public string? ApprovalResolvedBy { get; set; }
    public DateTimeOffset? ApprovalConsumedAt { get; set; }
    public string? ApprovalConsumedByDecisionId { get; set; }
    public string ExecutionGuidance { get; set; } = "";
    public string? CallerMessage { get; set; }
    public string? RetryGuidance { get; set; }
    public string? ApprovalOperationId { get; set; }
    public string? ApprovalCorrelationMode { get; set; }
}

public class AuditPolicyEvaluation
{
    public string PolicyId { get; set; } = "";
    public string PolicyName { get; set; } = "";
    public bool Matched { get; set; }
    public List<AuditConditionEvaluation> Conditions { get; set; } = new();
}

public class AuditConditionEvaluation
{
    public string Condition { get; set; } = "";
    public string Expected { get; set; } = "";
    public string Actual { get; set; } = "";
    public bool Passed { get; set; }
}
