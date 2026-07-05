using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record AuditEvent
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string DecisionId { get; init; }
    public required string RequestId { get; init; }

    public required Identity Identity { get; init; }
    public required Capability Capability { get; init; }
    public required Intent Intent { get; init; }
    public required Resource Resource { get; init; }

    public required DecisionType Decision { get; init; }
    public required EnforcementMode Mode { get; init; }

    public List<string> MatchedPolicies { get; init; } = new();

    public int LatencyMs { get; init; }
}