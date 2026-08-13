using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
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
    private readonly IdentityLoader _identityLoader;
    private readonly PolicyLoader _policyLoader;
    private readonly IGovernanceModeStore _governanceModeStore;
    private readonly IGovernanceWindowStore _governanceWindowStore;

    public CapabilityExplorerModel(
        ICapabilityCatalog capabilityCatalog,
        ICapabilityExplorer capabilityExplorer,
        IActivityStore activityStore,
        IAuditEventStore auditEventStore,
        IdentityLoader identityLoader,
        PolicyLoader policyLoader,
        IGovernanceModeStore governanceModeStore,
        IGovernanceWindowStore governanceWindowStore)
    {
        _capabilityCatalog = capabilityCatalog;
        _capabilityExplorer = capabilityExplorer;
        _activityStore = activityStore;
        _auditEventStore = auditEventStore;
        _identityLoader = identityLoader;
        _policyLoader = policyLoader;
        _governanceModeStore = governanceModeStore;
        _governanceWindowStore = governanceWindowStore;
    }

    public string? Query { get; private set; }
    public string? CapabilityId { get; private set; }
    public string? Owner { get; private set; }
    public string? Risk { get; private set; }
    public string? Category { get; private set; }
    public string? Technology { get; private set; }
    public string? Lifecycle { get; private set; }
    public IReadOnlyCollection<CapabilityCatalogEntry> SearchResults
        { get; private set; } = [];
    public CapabilityOverview? Overview { get; private set; }
    public CapabilityActivity? RuntimeActivity { get; private set; }
    public IReadOnlyCollection<AuditEvent> RecentDecisions { get; private set; } = [];
    public bool SearchWasRequested { get; private set; }
    public bool CapabilityWasRequested { get; private set; }
    public bool HasRuntimeActivity => RuntimeActivity?.TotalRequests > 0;
    public string RuntimeMode { get; private set; } = "LogOnly";
    public GovernanceWindow GovernanceWindow { get; private set; } = null!;
    public IReadOnlyCollection<CapabilityIdentityProfile> RelatedIdentities { get; private set; } = [];
    public IReadOnlyCollection<CapabilityPolicyProfile> RelatedPolicies { get; private set; } = [];
    public IReadOnlyCollection<CapabilityResourceProfile> RelatedResources { get; private set; } = [];
    public bool GovernanceWindowApplies => Overview is not null &&
        GovernanceWindow.Enabled && GovernanceWindow.AffectedCapabilities.Contains(
            Overview.CatalogEntry.Capability.Id, StringComparer.OrdinalIgnoreCase);
    public int ApprovalPolicyCount => RelatedPolicies.Count(policy =>
        string.Equals(policy.Effect, "RequireApproval", StringComparison.OrdinalIgnoreCase));

    public async Task OnGetAsync(
        string? q,
        string? capabilityId,
        string? owner,
        string? risk,
        string? category,
        string? technology,
        string? lifecycle,
        CancellationToken cancellationToken)
    {
        Query = q;
        CapabilityId = capabilityId;
        Owner = owner;
        Risk = risk;
        Category = category;
        Technology = technology;
        Lifecycle = lifecycle;
        RuntimeMode = _governanceModeStore.GetMode().ToString();
        GovernanceWindow = _governanceWindowStore.GetWindow();
        SearchWasRequested = !string.IsNullOrWhiteSpace(q) ||
            !string.IsNullOrWhiteSpace(owner) ||
            !string.IsNullOrWhiteSpace(risk) ||
            !string.IsNullOrWhiteSpace(category) ||
            !string.IsNullOrWhiteSpace(technology) ||
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
                    Technology = technology,
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
            .Take(10)
            .ToList();

        BuildRelationshipProfiles();
    }

    public string GetGovernanceSummary()
    {
        if (Overview is null) return string.Empty;
        var capability = Overview.CatalogEntry.Capability;
        var owner = string.IsNullOrWhiteSpace(capability.Owner)
            ? "an unspecified owner" : capability.Owner;
        var scope = RecentDecisions.Select(item => item.Environment)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var text = $"This is a {capability.RiskLevel.ToString().ToLowerInvariant()}-risk" +
            (string.IsNullOrWhiteSpace(scope) ? string.Empty : $" {scope}") +
            $" capability owned by {owner}. It is governed by {RelatedPolicies.Count} " +
            $"{Plural(RelatedPolicies.Count, "policy", "policies")}, used by " +
            $"{RelatedIdentities.Count} {Plural(RelatedIdentities.Count, "identity", "identities")}, " +
            $"and currently operating in {RuntimeMode} mode.";
        if (ApprovalPolicyCount > 0) text += $" {ApprovalPolicyCount} governing {Plural(ApprovalPolicyCount, "policy requires", "policies require")} approval.";
        if (GovernanceWindowApplies) text += $" The active {GovernanceWindow.Name} window participates in evaluations.";
        if (RecentDecisions.Any(item => item.Decision == DecisionType.Deny)) text += " Recent activity includes denied decisions.";
        if (RecentDecisions.Any(item => item.Decision == DecisionType.RequireApproval)) text += " Recent activity includes pending approval decisions.";
        return text;
    }

    public string GetImpactSummary() => RelatedResources.Count > 0
        ? $"Changes to this capability may affect {RelatedIdentities.Count} " +
          $"{Plural(RelatedIdentities.Count, "identity", "identities")} across " +
          $"{RelatedResources.Count} governed {Plural(RelatedResources.Count, "resource", "resources")}."
        : $"Changes to this capability may affect {RelatedIdentities.Count} " +
          $"{Plural(RelatedIdentities.Count, "identity", "identities")} and " +
          $"{RelatedPolicies.Count} governing {Plural(RelatedPolicies.Count, "policy", "policies")}; no related resources are currently projected.";

    private void BuildRelationshipProfiles()
    {
        if (Overview is null) return;
        var relationships = Overview.Relationships;
        var identities = _identityLoader.GetIdentities().ToDictionary(
            item => item.Name, StringComparer.OrdinalIgnoreCase);
        var policies = _policyLoader.GetPolicies().ToDictionary(
            item => item.Name, StringComparer.OrdinalIgnoreCase);

        RelatedIdentities = RelatedEntities(relationships, GovernanceEntityType.Identity)
            .Select(entity =>
            {
                identities.TryGetValue(entity.Id, out var definition);
                var recent = RecentDecisions.FirstOrDefault(item => string.Equals(
                    item.IdentityId, entity.Id, StringComparison.OrdinalIgnoreCase));
                return new CapabilityIdentityProfile(entity.Id,
                    definition?.Type ?? "Unknown", definition?.Description ?? string.Empty,
                    recent is null ? null : DecisionLabel(recent.Decision));
            }).ToList();
        RelatedPolicies = RelatedEntities(relationships, GovernanceEntityType.Policy)
            .Select(entity =>
            {
                policies.TryGetValue(entity.Id, out var policy);
                return new CapabilityPolicyProfile(entity.Id,
                    policy?.Decision ?? "Unknown", policy?.Environment ?? string.Empty,
                    policy?.Reason ?? string.Empty);
            }).ToList();
        RelatedResources = RelatedEntities(relationships, GovernanceEntityType.Resource)
            .Select(entity => new CapabilityResourceProfile(entity.Id,
                string.IsNullOrWhiteSpace(entity.Scope) ? "Not specified" : entity.Scope))
            .ToList();
    }

    private static IReadOnlyCollection<GovernanceEntityReference> RelatedEntities(
        IEnumerable<GovernanceRelationship> relationships, GovernanceEntityType type) =>
        relationships.SelectMany(item => new[] { item.From, item.To })
            .Where(item => item.Type == type)
            .DistinctBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList();

    private static string DecisionLabel(DecisionType decision) =>
        decision == DecisionType.RequireApproval ? "Pending Approval" : decision.ToString();
    private static string Plural(int count, string singular, string plural) => count == 1 ? singular : plural;

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

public sealed record CapabilityIdentityProfile(
    string Id, string Type, string Description, string? RecentDecision);

public sealed record CapabilityPolicyProfile(
    string Id, string Effect, string Environment, string Reason);

public sealed record CapabilityResourceProfile(string Id, string Environment);
