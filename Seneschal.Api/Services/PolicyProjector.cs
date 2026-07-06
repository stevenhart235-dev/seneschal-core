using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using ApiPolicy = Seneschal.Api.Models.Policy;

namespace Seneschal.Api.Services;

public sealed class PolicyProjector
{
    public IReadOnlyCollection<GovernanceRelationship> Project(
        IReadOnlyList<ApiPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        return policies
            .SelectMany(ProjectPolicy)
            .ToList();
    }

    private static IEnumerable<GovernanceRelationship> ProjectPolicy(
        ApiPolicy policy,
        int index)
    {
        var policyReference = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Policy,
            Id = policy.Name
        };
        var identityReference = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Identity,
            Id = policy.Identity
        };
        var capabilityReference = new GovernanceEntityReference
        {
            Type = GovernanceEntityType.Capability,
            Id = policy.Capability
        };

        yield return CreateRelationship(
            index,
            "policy-capability",
            policyReference,
            capabilityReference,
            GovernanceRelationshipType.PolicyAppliesToCapability);

        yield return CreateRelationship(
            index,
            "policy-identity",
            policyReference,
            identityReference,
            GovernanceRelationshipType.PolicyAppliesToIdentity);

        yield return CreateRelationship(
            index,
            "identity-capability",
            identityReference,
            capabilityReference,
            GovernanceRelationshipType.IdentityAssignedCapability);

        if (!string.IsNullOrWhiteSpace(policy.Environment))
        {
            yield return CreateRelationship(
                index,
                "policy-resource",
                policyReference,
                new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Resource,
                    Id = policy.Environment,
                    Scope = "environment"
                },
                GovernanceRelationshipType.PolicyAppliesToResource);
        }
    }

    private static GovernanceRelationship CreateRelationship(
        int policyIndex,
        string relationshipName,
        GovernanceEntityReference from,
        GovernanceEntityReference to,
        GovernanceRelationshipType type)
    {
        return new GovernanceRelationship
        {
            Id = $"policy-{policyIndex}-{relationshipName}",
            From = from,
            To = to,
            Type = type,
            Origin = GovernanceRelationshipOrigin.Declared,
            SourceSystem = "PolicyProjection"
        };
    }
}
