using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Services;

public sealed class GraphBuilder
{
    public async Task<GraphData> BuildAsync(
        IEnumerable<Capability> capabilities,
        IEnumerable<Identity> identities,
        IEnumerable<Policy> policies,
        IEnumerable<Resource> resources,
        IGovernanceGraph governanceGraph,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(governanceGraph);

        var nodes = new Dictionary<string, GraphNode>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var capability in capabilities)
        {
            var displayName = string.IsNullOrWhiteSpace(capability.DisplayName)
                ? capability.Name
                : capability.DisplayName;

            AddNode(
                nodes,
                new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Capability,
                    Id = capability.Id
                },
                displayName,
                new Dictionary<string, string>
                {
                    ["domainId"] = capability.Id,
                    ["displayName"] = displayName,
                    ["provider"] = capability.Provider,
                    ["owner"] = capability.Owner,
                    ["category"] = capability.Category,
                    ["description"] = capability.Description,
                    ["riskLevel"] = capability.RiskLevel.ToString(),
                    ["lifecycle"] = capability.Lifecycle,
                    ["documentationUrl"] = capability.DocumentationUrl,
                    ["tags"] = string.Join(", ", capability.Tags)
                });
        }

        foreach (var identity in identities)
        {
            AddNode(
                nodes,
                new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Identity,
                    Id = identity.Id
                },
                identity.Id,
                new Dictionary<string, string>
                {
                    ["domainId"] = identity.Id,
                    ["identityType"] = identity.Type.ToString(),
                    ["owner"] = identity.Owner,
                    ["environment"] = identity.Environment
                });
        }

        foreach (var policy in policies)
        {
            AddNode(
                nodes,
                new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Policy,
                    Id = policy.Id
                },
                policy.Name,
                new Dictionary<string, string>
                {
                    ["domainId"] = policy.Id,
                    ["effect"] = policy.Effect.ToString(),
                    ["reason"] = policy.Reason,
                    ["priority"] = policy.Priority.ToString()
                });
        }

        foreach (var resource in resources)
        {
            AddNode(
                nodes,
                new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Resource,
                    Id = resource.Id,
                    Scope = resource.Type
                },
                resource.Id,
                new Dictionary<string, string>
                {
                    ["domainId"] = resource.Id,
                    ["resourceType"] = resource.Type,
                    ["environment"] = resource.Environment ?? string.Empty
                });
        }

        var relationships = await governanceGraph.QueryAsync(
            new GovernanceRelationshipQuery(),
            cancellationToken);
        var edges = new List<GraphEdge>();

        foreach (var relationship in relationships)
        {
            var sourceId = AddNode(nodes, relationship.From);
            var targetId = AddNode(nodes, relationship.To);

            edges.Add(new GraphEdge
            {
                SourceId = sourceId,
                TargetId = targetId,
                RelationshipType = relationship.Type.ToString(),
                Label = ToLabel(relationship.Type)
            });
        }

        return new GraphData
        {
            Nodes = nodes.Values
                .OrderBy(node => node.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.Label, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Edges = edges
        };
    }

    private static string AddNode(
        IDictionary<string, GraphNode> nodes,
        GovernanceEntityReference entity,
        string? label = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var nodeId = ToNodeId(entity);

        if (!nodes.ContainsKey(nodeId))
        {
            nodes[nodeId] = new GraphNode
            {
                Id = nodeId,
                Label = string.IsNullOrWhiteSpace(label) ? entity.Id : label,
                Type = entity.Type.ToString(),
                Group = entity.Type.ToString(),
                Metadata = metadata ?? new Dictionary<string, string>
                {
                    ["domainId"] = entity.Id
                }
            };
        }

        return nodeId;
    }

    private static string ToNodeId(GovernanceEntityReference entity)
    {
        var type = entity.Type.ToString().ToLowerInvariant();

        return string.IsNullOrWhiteSpace(entity.Scope)
            ? $"{type}:{entity.Id}"
            : $"{type}:{entity.Scope}:{entity.Id}";
    }

    private static string ToLabel(GovernanceRelationshipType type)
    {
        return type switch
        {
            GovernanceRelationshipType.IdentityAssignedCapability =>
                "assigned",
            GovernanceRelationshipType.IdentityInvokedCapability =>
                "invoked",
            GovernanceRelationshipType.CapabilityTargetsResource =>
                "targets",
            GovernanceRelationshipType.PolicyAppliesToIdentity =>
                "applies to identity",
            GovernanceRelationshipType.PolicyAppliesToCapability =>
                "applies to capability",
            GovernanceRelationshipType.PolicyAppliesToResource =>
                "applies to resource",
            _ => type.ToString()
        };
    }
}
