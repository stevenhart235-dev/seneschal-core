using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record GovernanceRelationshipQuery
{
    public GovernanceEntityReference? Entity { get; init; }
    public GovernanceRelationshipDirection Direction { get; init; }
        = GovernanceRelationshipDirection.Any;
    public IReadOnlyCollection<GovernanceRelationshipType> RelationshipTypes
        { get; init; } = [];
    public IReadOnlyCollection<GovernanceRelationshipOrigin> Origins
        { get; init; } = [];
    public DateTimeOffset? ActiveAt { get; init; }
}
