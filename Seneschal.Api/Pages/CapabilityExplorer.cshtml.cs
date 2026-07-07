using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class CapabilityExplorerModel : PageModel
{
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly ICapabilityExplorer _capabilityExplorer;

    public CapabilityExplorerModel(
        ICapabilityCatalog capabilityCatalog,
        ICapabilityExplorer capabilityExplorer)
    {
        _capabilityCatalog = capabilityCatalog;
        _capabilityExplorer = capabilityExplorer;
    }

    public string? Query { get; private set; }
    public string? CapabilityId { get; private set; }
    public IReadOnlyCollection<CapabilityCatalogEntry> SearchResults
        { get; private set; } = [];
    public CapabilityOverview? Overview { get; private set; }
    public bool SearchWasRequested { get; private set; }
    public bool CapabilityWasRequested { get; private set; }

    public async Task OnGetAsync(
        string? q,
        string? capabilityId,
        CancellationToken cancellationToken)
    {
        Query = q;
        CapabilityId = capabilityId;
        SearchWasRequested = !string.IsNullOrWhiteSpace(q);
        CapabilityWasRequested = !string.IsNullOrWhiteSpace(capabilityId);

        if (SearchWasRequested)
        {
            SearchResults = await _capabilityCatalog.SearchAsync(
                new CapabilityCatalogQuery
                {
                    SearchText = q
                },
                cancellationToken);
        }

        if (!CapabilityWasRequested)
        {
            return;
        }

        Overview = await _capabilityExplorer.GetOverviewAsync(
            new CapabilityExplorerQuery
            {
                CapabilityId = capabilityId!
            },
            cancellationToken);
    }

    public IReadOnlyCollection<RelationshipGroup> GetRelationshipGroups()
    {
        if (Overview is null)
        {
            return [];
        }

        return Overview.Relationships
            .GroupBy(GetRelationshipGroupLabel)
            .OrderBy(group => GetRelationshipGroupOrder(group.Key))
            .ThenBy(group => group.Key)
            .Select(group => new RelationshipGroup(
                group.Key,
                group
                    .OrderBy(relationship => relationship.Id)
                    .Select(relationship => new RelationshipItem(
                        FormatRelationshipItem(relationship),
                        relationship.Origin.ToString(),
                        relationship.SourceSystem ?? "unknown"))
                    .ToList()))
            .ToList();
    }

    public IReadOnlyCollection<GraphNodeGroup> GetGraphNodeGroups()
    {
        if (Overview is null || Overview.Relationships.Count == 0)
        {
            return [];
        }

        var relatedEntities = Overview.Relationships
            .SelectMany(relationship => new[]
            {
                relationship.From,
                relationship.To
            })
            .Where(entity =>
                entity.Type != GovernanceEntityType.Capability ||
                !string.Equals(
                    entity.Id,
                    Overview.CatalogEntry.Capability.Id,
                    StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entity => FormatEntity(entity))
            .GroupBy(entity => entity.Type)
            .OrderBy(group => GetGraphGroupOrder(group.Key))
            .Select(group => new GraphNodeGroup(
                GraphGroupLabel(group.Key),
                group
                    .OrderBy(entity => entity.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(entity => new GraphNode(
                        entity.Type.ToString(),
                        FormatEntity(entity)))
                    .ToList()))
            .Where(group => group.Nodes.Count > 0)
            .ToList();

        return relatedEntities;
    }

    private static int GetGraphGroupOrder(GovernanceEntityType entityType)
    {
        return entityType switch
        {
            GovernanceEntityType.Identity => 0,
            GovernanceEntityType.Policy => 1,
            GovernanceEntityType.Resource => 2,
            _ => 3
        };
    }

    private static string GraphGroupLabel(GovernanceEntityType entityType)
    {
        return entityType switch
        {
            GovernanceEntityType.Identity => "Related Identities",
            GovernanceEntityType.Policy => "Related Policies",
            GovernanceEntityType.Resource => "Related Resources",
            _ => $"Related {entityType}"
        };
    }

    private static string GetRelationshipGroupLabel(
        GovernanceRelationship relationship)
    {
        return relationship.Type switch
        {
            GovernanceRelationshipType.IdentityAssignedCapability =>
                "Assigned Identities",
            GovernanceRelationshipType.IdentityInvokedCapability =>
                "Observed Identities",
            GovernanceRelationshipType.PolicyAppliesToCapability =>
                "Governing Policies",
            GovernanceRelationshipType.CapabilityTargetsResource or
                GovernanceRelationshipType.PolicyAppliesToResource =>
                "Resources",
            _ => relationship.Type.ToString()
        };
    }

    private static int GetRelationshipGroupOrder(string groupLabel)
    {
        return groupLabel switch
        {
            "Assigned Identities" => 0,
            "Observed Identities" => 1,
            "Governing Policies" => 2,
            "Resources" => 3,
            _ => 4
        };
    }

    private static string FormatRelationshipItem(
        GovernanceRelationship relationship)
    {
        return relationship.Type switch
        {
            GovernanceRelationshipType.IdentityAssignedCapability or
                GovernanceRelationshipType.IdentityInvokedCapability =>
                FormatRelatedEntity(relationship, GovernanceEntityType.Identity),
            GovernanceRelationshipType.PolicyAppliesToCapability =>
                FormatRelatedEntity(relationship, GovernanceEntityType.Policy),
            GovernanceRelationshipType.CapabilityTargetsResource or
                GovernanceRelationshipType.PolicyAppliesToResource =>
                FormatRelatedEntity(relationship, GovernanceEntityType.Resource),
            _ => $"{FormatEntity(relationship.From)} -> {FormatEntity(relationship.To)}"
        };
    }

    private static string FormatRelatedEntity(
        GovernanceRelationship relationship,
        GovernanceEntityType entityType)
    {
        var entity = relationship.From.Type == entityType
            ? relationship.From
            : relationship.To;

        return FormatEntity(entity);
    }

    private static string FormatEntity(GovernanceEntityReference entity)
    {
        var scope = string.IsNullOrWhiteSpace(entity.Scope)
            ? string.Empty
            : $"[{entity.Scope}]";

        return $"{entity.Type}{scope}:{entity.Id}";
    }
}

public sealed record RelationshipGroup(
    string Label,
    IReadOnlyCollection<RelationshipItem> Items);

public sealed record RelationshipItem(
    string Label,
    string Origin,
    string SourceSystem);

public sealed record GraphNodeGroup(
    string Label,
    IReadOnlyCollection<GraphNode> Nodes);

public sealed record GraphNode(
    string Type,
    string Label);
