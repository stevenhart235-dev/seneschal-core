using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class CapabilityExplorerModel : PageModel
{
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly ICapabilityExplorer _capabilityExplorer;
    private readonly IActivityStore _activityStore;
    private readonly IAuditEventStore _auditEventStore;

    public CapabilityExplorerModel(
        ICapabilityCatalog capabilityCatalog,
        ICapabilityExplorer capabilityExplorer,
        IActivityStore activityStore,
        IAuditEventStore auditEventStore)
    {
        _capabilityCatalog = capabilityCatalog;
        _capabilityExplorer = capabilityExplorer;
        _activityStore = activityStore;
        _auditEventStore = auditEventStore;
    }

    public string? Query { get; private set; }
    public string? CapabilityId { get; private set; }
    public string? Owner { get; private set; }
    public string? Risk { get; private set; }
    public string? Category { get; private set; }
    public string? Lifecycle { get; private set; }
    public IReadOnlyCollection<CapabilityCatalogEntry> SearchResults
        { get; private set; } = [];
    public CapabilityOverview? Overview { get; private set; }
    public CapabilityActivity? RuntimeActivity { get; private set; }
    public IReadOnlyCollection<AuditEvent> RecentDecisions { get; private set; } = [];
    public bool SearchWasRequested { get; private set; }
    public bool CapabilityWasRequested { get; private set; }
    public bool HasRuntimeActivity => RuntimeActivity?.TotalRequests > 0;

    public async Task OnGetAsync(
        string? q,
        string? capabilityId,
        string? owner,
        string? risk,
        string? category,
        string? lifecycle,
        CancellationToken cancellationToken)
    {
        Query = q;
        CapabilityId = capabilityId;
        Owner = owner;
        Risk = risk;
        Category = category;
        Lifecycle = lifecycle;
        SearchWasRequested = !string.IsNullOrWhiteSpace(q) ||
            !string.IsNullOrWhiteSpace(owner) ||
            !string.IsNullOrWhiteSpace(risk) ||
            !string.IsNullOrWhiteSpace(category) ||
            !string.IsNullOrWhiteSpace(lifecycle);
        CapabilityWasRequested = !string.IsNullOrWhiteSpace(capabilityId);

        if (SearchWasRequested)
        {
            SearchResults = await _capabilityCatalog.SearchAsync(
                new CapabilityCatalogQuery
                {
                    SearchText = q,
                    Owner = owner,
                    RiskLevels = Enum.TryParse<RiskLevel>(
                        risk,
                        ignoreCase: true,
                        out var riskLevel)
                            ? [riskLevel]
                            : [],
                    Category = category,
                    Lifecycle = lifecycle
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

        if (Overview is null)
        {
            return;
        }

        var activity = await _activityStore.GetSnapshotAsync(cancellationToken);
        RuntimeActivity = activity.Capabilities.FirstOrDefault(capability =>
            string.Equals(
                capability.CapabilityId,
                capabilityId,
                StringComparison.OrdinalIgnoreCase));

        RecentDecisions = (await _auditEventStore.GetRecentAsync(
                cancellationToken: cancellationToken))
            .Where(auditEvent => string.Equals(
                auditEvent.CapabilityId,
                capabilityId,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(auditEvent => auditEvent.TimestampUtc)
            .Take(5)
            .ToList();
    }

    public string GetRecommendation()
    {
        if (!HasRuntimeActivity)
        {
            return "Observe runtime activity before enforcing";
        }

        if (RuntimeActivity!.DeniedCount > 0)
        {
            return "Review denial patterns";
        }

        return "Review enforcement readiness";
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
                FormatCompactRelatedEntity(
                    relationship,
                    GovernanceEntityType.Identity),
            GovernanceRelationshipType.PolicyAppliesToCapability =>
                FormatCompactRelatedEntity(
                    relationship,
                    GovernanceEntityType.Policy),
            GovernanceRelationshipType.CapabilityTargetsResource or
                GovernanceRelationshipType.PolicyAppliesToResource =>
                FormatCompactRelatedEntity(
                    relationship,
                    GovernanceEntityType.Resource),
            _ => $"{FormatEntity(relationship.From)} -> {FormatEntity(relationship.To)}"
        };
    }

    private static string FormatCompactRelatedEntity(
        GovernanceRelationship relationship,
        GovernanceEntityType entityType)
    {
        var entity = relationship.From.Type == entityType
            ? relationship.From
            : relationship.To;

        return string.IsNullOrWhiteSpace(entity.Scope)
            ? entity.Id
            : $"{entity.Id} [{entity.Scope}]";
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
