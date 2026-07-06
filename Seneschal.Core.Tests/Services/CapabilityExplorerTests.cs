using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Seneschal.Core.Services;
using Xunit;

namespace Seneschal.Core.Tests.Services;

public sealed class CapabilityExplorerTests
{
    private static readonly DateTimeOffset FirstObserved =
        new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset LastObserved =
        new(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);

    private const string CapabilityId = "secrets.read";

    [Fact]
    public async Task GetOverviewAsync_UnknownCapabilityReturnsNull()
    {
        var explorer = CreateExplorer();

        var overview = await explorer.GetOverviewAsync(
            new CapabilityExplorerQuery
            {
                CapabilityId = "unknown"
            });

        Assert.Null(overview);
    }

    [Fact]
    public async Task GetOverviewAsync_IncludesCatalogEntryAndRelationshipSummary()
    {
        var explorer = CreateExplorer();

        var overview = await explorer.GetOverviewAsync(
            new CapabilityExplorerQuery
            {
                CapabilityId = CapabilityId
            });

        Assert.NotNull(overview);
        Assert.Equal(CapabilityId, overview.CatalogEntry.Capability.Id);
        Assert.Equal(2, overview.Summary.AssignedIdentityCount);
        Assert.Equal(1, overview.Summary.ObservedIdentityCount);
        Assert.Equal(2, overview.Summary.ResourceCount);
        Assert.Equal(1, overview.Summary.GoverningPolicyCount);
        Assert.Equal(FirstObserved, overview.Summary.FirstObservedAt);
        Assert.Equal(LastObserved, overview.Summary.LastObservedAt);
        Assert.Equal(
            [
                GovernanceRelationshipOrigin.Declared,
                GovernanceRelationshipOrigin.Observed,
                GovernanceRelationshipOrigin.Discovered,
                GovernanceRelationshipOrigin.Inferred
            ],
            overview.Summary.Origins);
    }

    [Fact]
    public async Task GetOverviewAsync_CountsRelatedEntitiesDistinctly()
    {
        var explorer = CreateExplorer();

        var overview = await explorer.GetOverviewAsync(
            new CapabilityExplorerQuery
            {
                CapabilityId = CapabilityId
            });

        Assert.NotNull(overview);
        Assert.Equal(2, overview.Summary.AssignedIdentityCount);
    }

    [Fact]
    public async Task GetOverviewAsync_AppliesActiveAtFiltering()
    {
        var explorer = CreateExplorer();

        var overview = await explorer.GetOverviewAsync(
            new CapabilityExplorerQuery
            {
                CapabilityId = CapabilityId,
                ActiveAt = new DateTimeOffset(
                    2026,
                    1,
                    15,
                    0,
                    0,
                    0,
                    TimeSpan.Zero)
            });

        Assert.NotNull(overview);
        Assert.DoesNotContain(
            overview.Relationships,
            relationship => relationship.Id == "expired-assignment");
        Assert.Equal(1, overview.Summary.AssignedIdentityCount);
    }

    private static CapabilityExplorer CreateExplorer()
    {
        var capability = new Capability
        {
            Id = CapabilityId,
            Name = "Read secrets",
            Provider = "azure",
            Category = "security",
            Description = "Read a secret.",
            RiskLevel = RiskLevel.High,
            Owner = "platform-security",
            Version = "1.0"
        };

        var catalog = new InMemoryCapabilityCatalog([capability]);
        var graph = new InMemoryGovernanceGraph(CreateRelationships());

        return new CapabilityExplorer(catalog, graph);
    }

    private static IReadOnlyCollection<GovernanceRelationship>
        CreateRelationships()
    {
        var capability = Reference(
            GovernanceEntityType.Capability,
            CapabilityId);
        var identityOne = Reference(
            GovernanceEntityType.Identity,
            "agent-1");
        var identityTwo = Reference(
            GovernanceEntityType.Identity,
            "agent-2");
        var resourceOne = Reference(
            GovernanceEntityType.Resource,
            "secret-1",
            "key-vault");
        var resourceTwo = Reference(
            GovernanceEntityType.Resource,
            "secret-2",
            "key-vault");
        var policy = Reference(
            GovernanceEntityType.Policy,
            "protect-secrets");

        return
        [
            Relationship(
                "active-assignment",
                identityOne,
                capability,
                GovernanceRelationshipType.IdentityAssignedCapability,
                GovernanceRelationshipOrigin.Declared),
            Relationship(
                "duplicate-assignment-evidence",
                identityOne,
                capability,
                GovernanceRelationshipType.IdentityAssignedCapability,
                GovernanceRelationshipOrigin.Observed),
            Relationship(
                "expired-assignment",
                identityTwo,
                capability,
                GovernanceRelationshipType.IdentityAssignedCapability,
                GovernanceRelationshipOrigin.Declared,
                validTo: new DateTimeOffset(
                    2026,
                    1,
                    10,
                    0,
                    0,
                    0,
                    TimeSpan.Zero)),
            Relationship(
                "invocation",
                identityOne,
                capability,
                GovernanceRelationshipType.IdentityInvokedCapability,
                GovernanceRelationshipOrigin.Observed,
                firstObservedAt: FirstObserved,
                lastObservedAt: LastObserved),
            Relationship(
                "resource-one",
                capability,
                resourceOne,
                GovernanceRelationshipType.CapabilityTargetsResource,
                GovernanceRelationshipOrigin.Observed,
                firstObservedAt: FirstObserved.AddDays(1),
                lastObservedAt: LastObserved.AddDays(-1)),
            Relationship(
                "resource-two",
                capability,
                resourceTwo,
                GovernanceRelationshipType.CapabilityTargetsResource,
                GovernanceRelationshipOrigin.Discovered),
            Relationship(
                "policy",
                policy,
                capability,
                GovernanceRelationshipType.PolicyAppliesToCapability,
                GovernanceRelationshipOrigin.Inferred)
        ];
    }

    private static GovernanceRelationship Relationship(
        string id,
        GovernanceEntityReference from,
        GovernanceEntityReference to,
        GovernanceRelationshipType type,
        GovernanceRelationshipOrigin origin,
        DateTimeOffset? validTo = null,
        DateTimeOffset? firstObservedAt = null,
        DateTimeOffset? lastObservedAt = null)
    {
        return new GovernanceRelationship
        {
            Id = id,
            From = from,
            To = to,
            Type = type,
            Origin = origin,
            ValidTo = validTo,
            FirstObservedAt = firstObservedAt,
            LastObservedAt = lastObservedAt
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
