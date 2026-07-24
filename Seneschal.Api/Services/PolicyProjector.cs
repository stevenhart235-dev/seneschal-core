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
        var identityReferences = policy.EffectiveIdentities.Select(identity =>
            new GovernanceEntityReference
            {
                Type = GovernanceEntityType.Identity,
                Id = identity
            }).ToList();
        var capabilityReferences = policy.EffectiveCapabilities.Select(capability =>
            new GovernanceEntityReference
            {
                Type = GovernanceEntityType.Capability,
                Id = capability
            }).ToList();

        foreach (var capabilityReference in capabilityReferences)
        {
            yield return CreateRelationship(
                index,
                $"policy-capability-{capabilityReference.Id}",
                policyReference,
                capabilityReference,
                GovernanceRelationshipType.PolicyAppliesToCapability);
        }

        foreach (var identityReference in identityReferences)
        {
            yield return CreateRelationship(
                index,
                $"policy-identity-{identityReference.Id}",
                policyReference,
                identityReference,
                GovernanceRelationshipType.PolicyAppliesToIdentity);

            foreach (var capabilityReference in capabilityReferences)
            {
                yield return CreateRelationship(
                    index,
                    $"identity-capability-{identityReference.Id}-{capabilityReference.Id}",
                    identityReference,
                    capabilityReference,
                    GovernanceRelationshipType.IdentityAssignedCapability);
            }
        }

        foreach (var environment in policy.EffectiveEnvironments)
        {
            yield return CreateRelationship(
                index,
                $"policy-resource-{environment}",
                policyReference,
                new GovernanceEntityReference
                {
                    Type = GovernanceEntityType.Resource,
                    Id = environment,
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
