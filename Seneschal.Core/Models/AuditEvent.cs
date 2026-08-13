using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record AuditEvent
{
    /// <summary>
    /// Gets the stable evidence identifier. A committed event and all of its
    /// fields are immutable; repeating an identical write with this identifier
    /// is idempotent, while different content is a conflict.
    /// </summary>
    public required string Id { get; init; }
    public string RequestId { get; init; } = string.Empty;
    public required DateTimeOffset TimestampUtc { get; init; }
    public required string IdentityId { get; init; }
    public required string CapabilityId { get; init; }
    public string RequestedAction { get; init; } = string.Empty;
    public Dictionary<string, string> RequestContext { get; init; } = new();
    public string ResourceId { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public required DecisionType Decision { get; init; }
    public required EnforcementMode EnforcementMode { get; init; }
    public string EffectiveAction { get; init; } = string.Empty;
    public List<string> MatchedPolicies { get; init; } = new();
    public List<string> Obligations { get; init; } = new();
    public required string Reason { get; init; }
    public int EvaluationDurationMs { get; init; }
    public string? GovernanceWindowName { get; init; }
    public string? GovernanceWindowMode { get; init; }
    public string? GovernanceWindowMessage { get; init; }
    public string? GovernanceWindowReason { get; init; }
    public DecisionType PolicyDecision { get; init; }
    public string PolicyReason { get; init; } = string.Empty;
    public List<PolicyEvaluation> PolicyEvaluations { get; init; } = new();
    public string? ApprovalId { get; init; }
    public string? ApprovalStatus { get; init; }
    public string? ApprovalAction { get; init; }
    public string? ApprovalRequestReason { get; init; }
    public DateTimeOffset? ApprovalResolvedAt { get; init; }
    public string? ApprovalResolvedBy { get; init; }
    public DateTimeOffset? ApprovalConsumedAt { get; init; }
    public string? ApprovalConsumedByDecisionId { get; init; }
    public string ExecutionGuidance { get; init; } = string.Empty;
    public string? CallerMessage { get; init; }
    public string? RetryGuidance { get; init; }
    public string? ApprovalOperationId { get; init; }
    public string? ApprovalCorrelationMode { get; init; }
    public string? GovernanceConfigurationFingerprint { get; init; }
}
