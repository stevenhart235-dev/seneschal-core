using System.Security.Cryptography;
using System.Text;
using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public sealed class ProposedGovernanceChangeCandidateService
{
    private readonly PolicyLoader _policies;
    private readonly GovernanceConfigurationFingerprintService _fingerprints;
    private readonly ProposedPolicyConfigurationService _configuration;
    public ProposedGovernanceChangeCandidateService(PolicyLoader policies,
        GovernanceConfigurationFingerprintService fingerprints,
        ProposedPolicyConfigurationService configuration)
    { _policies=policies; _fingerprints=fingerprints; _configuration=configuration; }

    public ProposedGovernanceChangeCandidateResult Offer(IdentityExposureRecommendation recommendation)
    {
        if (recommendation.RecommendationType != IdentityExposureRecommendationType.ReviewCurrentGovernanceRelationship ||
            recommendation.SourceFindingType != IdentityExposureFindingType.HighRiskConfiguredNotObserved ||
            recommendation.EvidenceCoverageStatus is not (IdentityEvidenceCoverageStatus.Full or IdentityEvidenceCoverageStatus.Partial) ||
            string.IsNullOrWhiteSpace(recommendation.CurrentConfigurationFingerprint))
            return None("Insufficient proposal eligibility.");
        if (recommendation.RelevantPolicies.Count != 1)
            return None(recommendation.RelevantPolicies.Count > 1
                ? "Multiple policies contribute to this governance relationship."
                : "No contributing policy can be located exactly.");
        var current=_fingerprints.GetCurrentFingerprint();
        if (!string.Equals(current,recommendation.CurrentConfigurationFingerprint,StringComparison.Ordinal))
            return None("Base configuration changed after the recommendation was produced.");
        var policy=_policies.GetPolicies().SingleOrDefault(item=>string.Equals(item.Name,
            recommendation.RelevantPolicies.Single(),StringComparison.OrdinalIgnoreCase));
        if(policy is null)return None("The contributing policy cannot be located exactly.");
        if(policy.EffectiveIdentities.Count!=1 || !string.Equals(policy.EffectiveIdentities[0],
            recommendation.IdentityId,StringComparison.OrdinalIgnoreCase))
            return None("The contributing policy targets multiple identities or a different identity.");
        if(policy.EffectiveIdentities.Concat(policy.EffectiveCapabilities).Concat(policy.EffectiveEnvironments)
            .Any(value=>value.Contains('*')))
            return None("Wildcard or unsupported targeting makes the proposed change ambiguous.");
        if(!policy.EffectiveCapabilities.Contains(recommendation.CapabilityId,StringComparer.OrdinalIgnoreCase))
            return None("The capability target cannot be located exactly in the contributing policy.");
        var key=$"{current}|{recommendation.IdentityId}|{recommendation.CapabilityId}|{policy.Name}";
        var id=$"proposal-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant()[..16]}";
        var proposal=new ProposedGovernanceChange { ContractVersion="v1",Revision=1,ProposalId=id,
            BaseGovernanceConfigurationFingerprint=current,Source=new(){RecommendationType=recommendation.RecommendationType.ToString(),
            FindingType=recommendation.SourceFindingType.ToString(),Identity=recommendation.IdentityId,
            Capability=recommendation.CapabilityId,EvidenceCoverage=recommendation.EvidenceCoverageStatus.ToString(),
            ObservationWindow=new(){StartUtc=recommendation.WindowStartUtc,EndUtc=recommendation.WindowEndUtc}},
            Change=new(){Operation="RemoveCapabilityFromPolicy",Policy=policy.Name,Capability=recommendation.CapabilityId}};
        return _configuration.Apply(proposal,_policies.GetPolicies()).IsValid
            ? new(true,proposal,null)
            : None("Removing the capability would leave invalid policy configuration.");
    }
    private static ProposedGovernanceChangeCandidateResult None(string reason)=>
        new(false,null,$"No deterministic proposal available: {reason}");
}
public sealed record ProposedGovernanceChangeCandidateResult(bool IsAvailable,
    ProposedGovernanceChange? Proposal,string? Reason);
