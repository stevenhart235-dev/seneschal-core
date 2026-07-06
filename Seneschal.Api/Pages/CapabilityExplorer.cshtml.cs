using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class CapabilityExplorerModel : PageModel
{
    private readonly ICapabilityExplorer _capabilityExplorer;

    public CapabilityExplorerModel(ICapabilityExplorer capabilityExplorer)
    {
        _capabilityExplorer = capabilityExplorer;
    }

    public string? CapabilityId { get; private set; }
    public CapabilityOverview? Overview { get; private set; }
    public bool CapabilityWasRequested { get; private set; }

    public async Task OnGetAsync(
        string? capabilityId,
        CancellationToken cancellationToken)
    {
        CapabilityId = capabilityId;
        CapabilityWasRequested = !string.IsNullOrWhiteSpace(capabilityId);

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
