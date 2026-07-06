using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryGovernanceGraphTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly GovernanceEntityReference _identity = Reference(
        GovernanceEntityType.Identity,
        "agent-1");

    private readonly GovernanceEntityReference _capability = Reference(
        GovernanceEntityType.Capability,
        "secrets.read");

    private readonly GovernanceEntityReference _resource = Reference(
        GovernanceEntityType.Resource,
        "production-secret",
        "key-vault");

    private readonly GovernanceEntityReference _policy = Reference(
        GovernanceEntityType.Policy,
        "protect-secrets");

    [Fact]
    public async Task QueryAsync_FiltersByEntityInEitherDirection()
    {
        var graph = CreateGraph();

        var relationships = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                Entity = Reference(
                    GovernanceEntityType.Capability,
                    "SECRETS.READ")
            });

        Assert.Equal(
            ["assigned", "targets", "policy-capability"],
            relationships.Select(relationship => relationship.Id));
    }

    [Fact]
    public async Task QueryAsync_FiltersByDirection()
    {
        var graph = CreateGraph();

        var outgoing = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                Entity = _capability,
                Direction = GovernanceRelationshipDirection.Outgoing
            });
        var incoming = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                Entity = _capability,
                Direction = GovernanceRelationshipDirection.Incoming
            });

        Assert.Equal(
            ["targets"],
            outgoing.Select(relationship => relationship.Id));
        Assert.Equal(
            ["assigned", "policy-capability"],
            incoming.Select(relationship => relationship.Id));
    }

    [Fact]
    public async Task QueryAsync_FiltersByRelationshipType()
    {
        var graph = CreateGraph();

        var relationships = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                RelationshipTypes =
                [
                    GovernanceRelationshipType.PolicyAppliesToCapability
                ]
            });

        var relationship = Assert.Single(relationships);
        Assert.Equal("policy-capability", relationship.Id);
    }

    [Fact]
    public async Task QueryAsync_FiltersByOrigin()
    {
        var graph = CreateGraph();

        var relationships = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                Origins = [GovernanceRelationshipOrigin.Observed]
            });

        var relationship = Assert.Single(relationships);
        Assert.Equal("targets", relationship.Id);
    }

    [Fact]
    public async Task QueryAsync_FiltersByActiveAtUsingHalfOpenInterval()
    {
        var graph = CreateGraph();

        var whileActive = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                ActiveAt = Start.AddDays(5)
            });
        var atValidTo = await graph.QueryAsync(
            new GovernanceRelationshipQuery
            {
                ActiveAt = Start.AddDays(10)
            });

        Assert.Contains(
            whileActive,
            relationship => relationship.Id == "assigned");
        Assert.DoesNotContain(
            atValidTo,
            relationship => relationship.Id == "assigned");
        Assert.Contains(
            atValidTo,
            relationship => relationship.Id == "targets");
    }

    private InMemoryGovernanceGraph CreateGraph()
    {
        return new InMemoryGovernanceGraph(
        [
            Relationship(
                "assigned",
                _identity,
                _capability,
                GovernanceRelationshipType.IdentityAssignedCapability,
                GovernanceRelationshipOrigin.Declared,
                Start,
                Start.AddDays(10)),
            Relationship(
                "targets",
                _capability,
                _resource,
                GovernanceRelationshipType.CapabilityTargetsResource,
                GovernanceRelationshipOrigin.Observed),
            Relationship(
                "policy-capability",
                _policy,
                _capability,
                GovernanceRelationshipType.PolicyAppliesToCapability,
                GovernanceRelationshipOrigin.Inferred)
        ]);
    }

    private static GovernanceRelationship Relationship(
        string id,
        GovernanceEntityReference from,
        GovernanceEntityReference to,
        GovernanceRelationshipType type,
        GovernanceRelationshipOrigin origin,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validTo = null)
    {
        return new GovernanceRelationship
        {
            Id = id,
            From = from,
            To = to,
            Type = type,
            Origin = origin,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
    }

    private static GovernanceEntityReference Reference(
        GovernanceEntityType type,
        string id,
        string? scope = null)
    {
        return new GovernanceEntityReference
        {
            Type = type,
            Id = id,
            Scope = scope
        };
    }
}
