using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record CapabilityOverviewSummary
{
    public int AssignedIdentityCount { get; init; }
    public int ObservedIdentityCount { get; init; }
    public int ResourceCount { get; init; }
    public int GoverningPolicyCount { get; init; }
    public DateTimeOffset? FirstObservedAt { get; init; }
    public DateTimeOffset? LastObservedAt { get; init; }
    public IReadOnlyCollection<GovernanceRelationshipOrigin> Origins
        { get; init; } = [];
}
