using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record GovernanceRelationship
{
    public required string Id { get; init; }
    public required GovernanceEntityReference From { get; init; }
    public required GovernanceEntityReference To { get; init; }
    public required GovernanceRelationshipType Type { get; init; }
    public required GovernanceRelationshipOrigin Origin { get; init; }

    public string? SourceSystem { get; init; }
    public decimal? Confidence { get; init; }
    public DateTimeOffset? FirstObservedAt { get; init; }
    public DateTimeOffset? LastObservedAt { get; init; }
    public DateTimeOffset? ValidFrom { get; init; }
    public DateTimeOffset? ValidTo { get; init; }

    public IReadOnlyCollection<string> EvidenceIds { get; init; } = [];
    public IReadOnlyDictionary<string, string> Attributes { get; init; }
        = new Dictionary<string, string>();
}
