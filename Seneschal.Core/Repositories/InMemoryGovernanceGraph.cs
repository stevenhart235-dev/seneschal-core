using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryGovernanceGraph : IGovernanceGraph
{
    private readonly IReadOnlyCollection<GovernanceRelationship> _relationships;

    public InMemoryGovernanceGraph(
        IEnumerable<GovernanceRelationship> relationships)
    {
        ArgumentNullException.ThrowIfNull(relationships);

        var relationshipList = relationships.ToList();

        _ = relationshipList.ToDictionary(
            relationship => relationship.Id,
            StringComparer.OrdinalIgnoreCase);
        _relationships = relationshipList;
    }

    public Task<IReadOnlyCollection<GovernanceRelationship>> QueryAsync(
        GovernanceRelationshipQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<GovernanceRelationship> matches = _relationships;

        if (query.Entity is not null)
        {
            matches = matches.Where(relationship =>
                MatchesEntityAndDirection(
                    relationship,
                    query.Entity,
                    query.Direction));
        }

        if (query.RelationshipTypes.Count > 0)
        {
            matches = matches.Where(relationship =>
                query.RelationshipTypes.Contains(relationship.Type));
        }

        if (query.Origins.Count > 0)
        {
            matches = matches.Where(relationship =>
                query.Origins.Contains(relationship.Origin));
        }

        if (query.ActiveAt is { } activeAt)
        {
            matches = matches.Where(relationship =>
                (!relationship.ValidFrom.HasValue ||
                    relationship.ValidFrom.Value <= activeAt) &&
                (!relationship.ValidTo.HasValue ||
                    activeAt < relationship.ValidTo.Value));
        }

        return Task.FromResult<IReadOnlyCollection<GovernanceRelationship>>(
            matches.ToList());
    }

    private static bool MatchesEntityAndDirection(
        GovernanceRelationship relationship,
        GovernanceEntityReference entity,
        GovernanceRelationshipDirection direction)
    {
        return direction switch
        {
            GovernanceRelationshipDirection.Outgoing =>
                ReferencesSameEntity(relationship.From, entity),
            GovernanceRelationshipDirection.Incoming =>
                ReferencesSameEntity(relationship.To, entity),
            GovernanceRelationshipDirection.Any =>
                ReferencesSameEntity(relationship.From, entity) ||
                ReferencesSameEntity(relationship.To, entity),
            _ => false
        };
    }

    private static bool ReferencesSameEntity(
        GovernanceEntityReference left,
        GovernanceEntityReference right)
    {
        return left.Type == right.Type &&
            string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                left.Scope,
                right.Scope,
                StringComparison.OrdinalIgnoreCase);
    }
}
