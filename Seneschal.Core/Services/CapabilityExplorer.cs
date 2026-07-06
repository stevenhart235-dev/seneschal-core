using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class CapabilityExplorer : ICapabilityExplorer
{
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly IGovernanceGraph _governanceGraph;

    public CapabilityExplorer(
        ICapabilityCatalog capabilityCatalog,
        IGovernanceGraph governanceGraph)
    {
        _capabilityCatalog = capabilityCatalog;
        _governanceGraph = governanceGraph;
    }

    public async Task<CapabilityOverview?> GetOverviewAsync(
        CapabilityExplorerQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.CapabilityId);

        var catalogEntry = await _capabilityCatalog.GetByIdAsync(
            query.CapabilityId,
            cancellationToken);

        if (catalogEntry is null)
        {
            return null;
        }

        var relationships = await _governanceGraph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                Entity = new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Capability,
                    Id = catalogEntry.Capability.Id
                },
                Direction = GovernanceRelationshipDirection.Any,
                ActiveAt = query.ActiveAt
            },
            cancellationToken);

        return new CapabilityOverview
        {
            CatalogEntry = catalogEntry,
            Relationships = relationships,
            Summary = CreateSummary(relationships)
        };
    }

    private static CapabilityOverviewSummary CreateSummary(
        IReadOnlyCollection<GovernanceRelationship> relationships)
    {
        return new CapabilityOverviewSummary
        {
            AssignedIdentityCount = CountDistinctRelatedEntities(
                relationships,
                GovernanceRelationshipType.IdentityAssignedCapability,
                GovernanceEntityType.Identity),
            ObservedIdentityCount = CountDistinctRelatedEntities(
                relationships,
                GovernanceRelationshipType.IdentityInvokedCapability,
                GovernanceEntityType.Identity),
            ResourceCount = CountDistinctRelatedEntities(
                relationships,
                GovernanceRelationshipType.CapabilityTargetsResource,
                GovernanceEntityType.Resource),
            GoverningPolicyCount = CountDistinctRelatedEntities(
                relationships,
                GovernanceRelationshipType.PolicyAppliesToCapability,
                GovernanceEntityType.Policy),
            FirstObservedAt = relationships
                .Where(relationship => relationship.FirstObservedAt.HasValue)
                .Select(relationship => relationship.FirstObservedAt)
                .Min(),
            LastObservedAt = relationships
                .Where(relationship => relationship.LastObservedAt.HasValue)
                .Select(relationship => relationship.LastObservedAt)
                .Max(),
            Origins = relationships
                .Select(relationship => relationship.Origin)
                .Distinct()
                .ToList()
        };
    }

    private static int CountDistinctRelatedEntities(
        IEnumerable<GovernanceRelationship> relationships,
        GovernanceRelationshipType relationshipType,
        GovernanceEntityType entityType)
    {
        return relationships
            .Where(relationship => relationship.Type == relationshipType)
            .SelectMany(relationship => new[]
            {
                relationship.From,
                relationship.To
            })
            .Where(entity => entity.Type == entityType)
            .Select(entity => $"{entity.Type}\0{entity.Scope}\0{entity.Id}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }
}
