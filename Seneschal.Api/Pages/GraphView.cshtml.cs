using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class GraphViewModel : PageModel
{
    private readonly ICapabilityCatalog _catalog;
    private readonly ICapabilityExplorer _explorer;
    private readonly IGovernanceGraph _graph;

    public GraphViewModel(
        ICapabilityCatalog catalog,
        ICapabilityExplorer explorer,
        IGovernanceGraph graph)
    {
        _catalog = catalog;
        _explorer = explorer;
        _graph = graph;
    }

    public string? CapabilityId { get; private set; }
    public CapabilityOverview? Overview { get; private set; }
    public IReadOnlyCollection<CapabilityCatalogEntry> Capabilities
        { get; private set; } = [];
    public IReadOnlyCollection<GovernanceRelationship> FocusedRelationships
        { get; private set; } = [];

    public async Task OnGetAsync(
        string? capabilityId,
        CancellationToken cancellationToken)
    {
        Capabilities = (await _catalog.SearchAsync(
                new CapabilityCatalogQuery(),
                cancellationToken))
            .OrderBy(entry => entry.Capability.DisplayName)
            .ThenBy(entry => entry.Capability.Id)
            .ToList();

        CapabilityId = string.IsNullOrWhiteSpace(capabilityId)
            ? Capabilities.FirstOrDefault()?.Capability.Id
            : capabilityId;

        if (string.IsNullOrWhiteSpace(CapabilityId))
        {
            return;
        }

        Overview = await _explorer.GetOverviewAsync(
            new CapabilityExplorerQuery
            {
                CapabilityId = CapabilityId
            },
            cancellationToken);

        if (Overview is null)
        {
            return;
        }

        var allRelationships = await _graph.QueryAsync(
            new GovernanceRelationshipQuery(),
            cancellationToken);
        var directlyRelated = allRelationships
            .Where(relationship => ReferencesCapability(
                relationship,
                CapabilityId))
            .ToList();
        var relatedPolicyIds = directlyRelated
            .SelectMany(relationship => new[] { relationship.From, relationship.To })
            .Where(entity => entity.Type == GovernanceEntityType.Policy)
            .Select(entity => entity.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        FocusedRelationships = directlyRelated
            .Concat(allRelationships.Where(relationship =>
                ReferencesPolicy(relationship, relatedPolicyIds) &&
                ReferencesType(
                    relationship,
                    GovernanceEntityType.Identity,
                    GovernanceEntityType.Resource)))
            .DistinctBy(relationship => relationship.Id)
            .ToList();
    }

    public IReadOnlyCollection<FallbackRelationshipGroup>
        GetFallbackRelationshipGroups()
    {
        if (Overview is null)
        {
            return [];
        }

        return FocusedRelationships
            .SelectMany(relationship => new[]
            {
                relationship.From,
                relationship.To
            })
            .Where(entity => entity.Type is
                GovernanceEntityType.Identity or
                GovernanceEntityType.Policy or
                GovernanceEntityType.Resource)
            .DistinctBy(entity => $"{entity.Type}\0{entity.Scope}\0{entity.Id}")
            .GroupBy(entity => entity.Type)
            .OrderBy(group => group.Key)
            .Select(group => new FallbackRelationshipGroup(
                group.Key.ToString(),
                group
                    .OrderBy(entity => entity.Id)
                    .Select(entity => entity.Id)
                    .ToList()))
            .ToList();
    }

    private static bool ReferencesCapability(
        GovernanceRelationship relationship,
        string capabilityId)
    {
        return new[] { relationship.From, relationship.To }.Any(entity =>
            entity.Type == GovernanceEntityType.Capability &&
            string.Equals(entity.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ReferencesPolicy(
        GovernanceRelationship relationship,
        IReadOnlySet<string> policyIds)
    {
        return new[] { relationship.From, relationship.To }.Any(entity =>
            entity.Type == GovernanceEntityType.Policy && policyIds.Contains(entity.Id));
    }

    private static bool ReferencesType(
        GovernanceRelationship relationship,
        params GovernanceEntityType[] types)
    {
        return new[] { relationship.From, relationship.To }.Any(entity =>
            types.Contains(entity.Type));
    }
}

public sealed record FallbackRelationshipGroup(
    string Type,
    IReadOnlyCollection<string> EntityIds);
