using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record ApprovalRecord
{
    public required string Id { get; init; }
    public required string IdentityId { get; init; }
    public required string CapabilityId { get; init; }
    public required string Environment { get; init; }
    public required string ResourceId { get; init; }
    public string? OperationId { get; init; }
    public ApprovalCorrelationMode CorrelationMode { get; init; } =
        ApprovalCorrelationMode.LegacyContext;
    public required string RequestReason { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public ApprovalStatus Status { get; init; } = ApprovalStatus.Pending;
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTimeOffset? ConsumedAt { get; init; }
    public string? ConsumedByDecisionId { get; init; }
}

public sealed record ApprovalLookupResult(ApprovalRecord Record, bool Created);
