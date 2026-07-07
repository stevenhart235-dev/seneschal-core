using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Core.Tests.Services;

public sealed class GraphBuilderTests
{
    [Fact]
    public async Task BuildAsync_RepresentsConfiguredEntitiesAsNodes()
    {
        var graph = await CreateBuilder().BuildAsync(
            CreateCapabilities(),
            CreateIdentities(),
            CreatePolicies(),
            CreateResources(),
            CreateGovernanceGraph());

        Assert.Contains(
            graph.Nodes,
            node => node.Id == "capability:app.deploy" &&
                node.Type == "Capability" &&
                node.Label == "Deploy application");
        Assert.Contains(
            graph.Nodes,
            node => node.Id == "identity:payment-agent" &&
                node.Type == "Identity");
        Assert.Contains(
            graph.Nodes,
            node => node.Id == "policy:prod-deploy" &&
                node.Type == "Policy");
        Assert.Contains(
            graph.Nodes,
            node => node.Id == "resource:environment:production" &&
                node.Type == "Resource");
    }

    [Fact]
    public async Task BuildAsync_RepresentsGovernanceRelationshipsAsEdges()
    {
        var graph = await CreateBuilder().BuildAsync(
            CreateCapabilities(),
            CreateIdentities(),
            CreatePolicies(),
            CreateResources(),
            CreateGovernanceGraph());

        Assert.Contains(
            graph.Edges,
            edge => edge.SourceId == "policy:prod-deploy" &&
                edge.TargetId == "capability:app.deploy" &&
                edge.RelationshipType == "PolicyAppliesToCapability");
        Assert.Contains(
            graph.Edges,
            edge => edge.SourceId == "identity:payment-agent" &&
                edge.TargetId == "capability:app.deploy" &&
                edge.RelationshipType == "IdentityAssignedCapability");
        Assert.Contains(
            graph.Edges,
            edge => edge.SourceId == "policy:prod-deploy" &&
                edge.TargetId == "resource:environment:production" &&
                edge.Label == "applies to resource");
    }

    [Fact]
    public async Task BuildAsync_AddsNodesDiscoveredFromRelationships()
    {
        var graph = await CreateBuilder().BuildAsync(
            [],
            [],
            [],
            [],
            CreateGovernanceGraph());

        Assert.Contains(
            graph.Nodes,
            node => node.Id == "capability:app.deploy");
        Assert.Contains(
            graph.Nodes,
            node => node.Id == "identity:payment-agent");
        Assert.Contains(
            graph.Nodes,
            node => node.Id == "policy:prod-deploy");
        Assert.Contains(
            graph.Nodes,
            node => node.Id == "resource:environment:production");
    }

    private static GraphBuilder CreateBuilder()
    {
        return new GraphBuilder();
    }

    private static IReadOnlyCollection<Capability> CreateCapabilities()
    {
        return
        [
            new Capability
            {
                Id = "app.deploy",
                Name = "Deploy application",
                Provider = "sample",
                Category = "deployment",
                Description = "Deploys an application.",
                RiskLevel = RiskLevel.Medium
            }
        ];
    }

    private static IReadOnlyCollection<Identity> CreateIdentities()
    {
        return
        [
            new Identity
            {
                Id = "payment-agent",
                Type = IdentityType.Agent,
                Owner = "Payments",
                Environment = "production"
            }
        ];
    }

    private static IReadOnlyCollection<Policy> CreatePolicies()
    {
        return
        [
            new Policy
            {
                Id = "prod-deploy",
                Name = "Production deploy",
                Effect = DecisionType.Allow,
                Reason = "Allowed in production.",
                Priority = 10
            }
        ];
    }

    private static IReadOnlyCollection<Resource> CreateResources()
    {
        return
        [
            new Resource
            {
                Type = "environment",
                Id = "production",
                Environment = "production"
            }
        ];
    }

    private static InMemoryGovernanceGraph CreateGovernanceGraph()
    {
        var policy = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Policy,
            Id = "prod-deploy"
        };
        var identity = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Identity,
            Id = "payment-agent"
        };
        var capability = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Capability,
            Id = "app.deploy"
        };
        var resource = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Resource,
            Id = "production",
            Scope = "environment"
        };

        return new InMemoryGovernanceGraph(
        [
            CreateRelationship(
                "policy-capability",
                policy,
                capability,
                GovernanceRelationshipType.PolicyAppliesToCapability),
            CreateRelationship(
                "policy-identity",
                policy,
                identity,
                GovernanceRelationshipType.PolicyAppliesToIdentity),
            CreateRelationship(
                "identity-capability",
                identity,
                capability,
                GovernanceRelationshipType.IdentityAssignedCapability),
            CreateRelationship(
                "policy-resource",
                policy,
                resource,
                GovernanceRelationshipType.PolicyAppliesToResource)
        ]);
    }

    private static GovernanceRelationship CreateRelationship(
        string id,
        GovernanceEntityReference from,
        GovernanceEntityReference to,
        GovernanceRelationshipType type)
    {
        return new GovernanceRelationship
        {
            Id = id,
            From = from,
            To = to,
            Type = type,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "Test"
        };
    }
}
