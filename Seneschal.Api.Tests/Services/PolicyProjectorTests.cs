using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class PolicyProjectorTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly PolicyLoader _loader;
    private readonly PolicyProjector _projector = new();

    public PolicyProjectorTests(ApiApplicationFactory factory)
    {
        _ = factory;
        _loader = new PolicyLoader();
    }

    [Fact]
    public void Project_CreatesPolicyRelationshipsForCapability()
    {
        var relationships = _projector.Project(_loader.GetPolicies());

        var capabilityRelationships = relationships
            .Where(relationship =>
                relationship.Type == GovernanceRelationshipType.PolicyAppliesToCapability &&
                relationship.To.Id == "DeployApplication")
            .ToList();

        Assert.Equal(2, capabilityRelationships.Count);
        Assert.All(
            capabilityRelationships,
            relationship =>
            {
                Assert.Equal(GovernanceEntityType.Policy, relationship.From.Type);
                Assert.Equal(GovernanceEntityType.Capability, relationship.To.Type);
                Assert.Equal(
                    GovernanceRelationshipOrigin.Declared,
                    relationship.Origin);
                Assert.Equal("PolicyProjection", relationship.SourceSystem);
            });
    }

    [Fact]
    public void Project_CreatesIdentityAndResourceRelationships()
    {
        var relationships = _projector.Project(_loader.GetPolicies());

        Assert.Contains(
            relationships,
            relationship =>
                relationship.Type == GovernanceRelationshipType.PolicyAppliesToIdentity &&
                relationship.From.Id == "Developers can deploy to dev" &&
                relationship.To.Id == "Developer");

        Assert.Contains(
            relationships,
            relationship =>
                relationship.Type == GovernanceRelationshipType.IdentityAssignedCapability &&
                relationship.From.Id == "Developer" &&
                relationship.To.Id == "DeployApplication");

        Assert.Contains(
            relationships,
            relationship =>
                relationship.Type == GovernanceRelationshipType.PolicyAppliesToResource &&
                relationship.From.Id == "Developers can deploy to dev" &&
                relationship.To.Id == "dev" &&
                relationship.To.Scope == "environment");
    }
}
