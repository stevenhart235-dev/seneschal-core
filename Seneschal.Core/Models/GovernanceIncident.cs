using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record GovernanceIncident
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required GovernanceIncidentSeverity Severity { get; init; }

    public required string CapabilityId { get; init; }

    public required string IdentityId { get; init; }

    public required string DecisionReason { get; init; }

    public string MatchedPolicy { get; init; } = string.Empty;

    public required DateTimeOffset FirstSeenUtc { get; init; }

    public required DateTimeOffset LastSeenUtc { get; init; }

    public required int OccurrenceCount { get; init; }

    public required GovernanceIncidentStatus CurrentStatus { get; init; }

    public long OperatorStateVersion { get; init; }
}
