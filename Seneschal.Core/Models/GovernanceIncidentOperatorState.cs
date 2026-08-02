using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record GovernanceIncidentOperatorState
{
    public required string IncidentId { get; init; }
    public required GovernanceIncidentStatus Status { get; init; }
    public long Version { get; init; }
}
