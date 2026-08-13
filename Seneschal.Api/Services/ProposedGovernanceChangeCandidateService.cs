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
            recommendation.RelevantPolicies.Count != 1 ||
            string.IsNullOrWhiteSpace(recommendation.CurrentConfigurationFingerprint)) return None();
        var current = _fingerprints.GetCurrentFingerprint();
        if (!string.Equals(current, recommendation.CurrentConfigurationFingerprint, StringComparison.Ordinal)) return None();
        var policy = _policies.GetPolicies().SingleOrDefault(item => string.Equals(item.Name,
            recommendation.RelevantPolicies.Single(), StringComparison.OrdinalIgnoreCase));
        if (policy is null || policy.EffectiveIdentities.Count != 1 ||
            !string.Equals(policy.EffectiveIdentities[0], recommendation.IdentityId, StringComparison.OrdinalIgnoreCase) ||
            policy.EffectiveIdentities.Concat(policy.EffectiveCapabilities).Concat(policy.EffectiveEnvironments).Any(value => value.Contains('*')) ||
            !policy.EffectiveCapabilities.Contains(recommendation.CapabilityId, StringComparer.OrdinalIgnoreCase)) return None();
        var key = $"{current}|{recommendation.IdentityId}|{recommendation.CapabilityId}|{policy.Name}";
        var id = $"proposal-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant()[..16]}";
        var proposal = new ProposedGovernanceChange { ContractVersion="v1", Revision=1,
            ProposalId=id, BaseGovernanceConfigurationFingerprint=current,
            Source=new() { RecommendationType=recommendation.RecommendationType.ToString(),
                FindingType=recommendation.SourceFindingType.ToString(), Identity=recommendation.IdentityId,
                Capability=recommendation.CapabilityId, EvidenceCoverage=recommendation.EvidenceCoverageStatus.ToString(),
                ObservationWindow=new() { StartUtc=recommendation.WindowStartUtc, EndUtc=recommendation.WindowEndUtc } },
            Change=new() { Operation="RemoveCapabilityFromPolicy", Policy=policy.Name,
                Capability=recommendation.CapabilityId } };
        return _configuration.Apply(proposal, _policies.GetPolicies()).IsValid
            ? new(true, proposal, null) : None();
    }
    private static ProposedGovernanceChangeCandidateResult None() =>
        new(false, null, "No deterministic proposal available");
}
public sealed record ProposedGovernanceChangeCandidateResult(bool IsAvailable,
    ProposedGovernanceChange? Proposal, string? Reason);
