namespace Seneschal.Api.Services;

public sealed class IdentityExposureRecommendationService
{
    public IReadOnlyCollection<IdentityExposureRecommendation> Generate(
        IEnumerable<IdentityExposureFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings.Select(Create)
            .OrderBy(item => TypeOrder(item.RecommendationType))
            .ThenBy(item => RiskOrder(item.CapabilityRisk))
            .ThenBy(item => item.CapabilityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.IdentityId, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(item => (item.RecommendationType,
                item.IdentityId.ToUpperInvariant(),
                item.CapabilityId.ToUpperInvariant()))
            .ToList();
    }

    private static IdentityExposureRecommendation Create(
        IdentityExposureFinding finding) => finding.FindingType switch
        {
            IdentityExposureFindingType.HighRiskConfiguredNotObserved => New(
                IdentityExposureRecommendationType.ReviewCurrentGovernanceRelationship,
                "Review current governance relationship",
                finding.EvidenceCoverageStatus == IdentityEvidenceCoverageStatus.Full
                    ? "No activity was observed during the fully covered selected period. Review the policies and capability context that produced this finding."
                    : "No use was found in retained evidence. Evidence does not cover the complete requested period, so review the policies and capability context with that limitation in mind.",
                "Review whether this capability should remain represented in the identity's current governance context.",
                "The available facts do not establish business necessity or justify a specific policy change.",
                finding),
            IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext => New(
                IdentityExposureRecommendationType.ReviewHistoricalActivityAgainstCurrentGovernance,
                "Review historical activity against current governance",
                "Recorded activity exists outside the identity's current configured governance context. Compare the retained evidence and available configuration provenance without assuming why they differ.",
                "Investigate whether current configuration intentionally differs from the configuration under which this activity occurred.",
                "The available facts do not establish the cause of the difference or justify a specific policy change.",
                finding),
            IdentityExposureFindingType.HistoricalConfigurationDiffers => New(
                IdentityExposureRecommendationType.ReviewHistoricalConfigurationChanges,
                "Review evaluation-relevant configuration changes",
                "Historical evidence was recorded under an evaluation-relevant configuration fingerprint different from the current fingerprint. The fingerprints do not identify a specific changed policy.",
                "Review configuration changes relevant to the historical evaluation period alongside the recorded evidence.",
                "The historical decision is not automatically invalid, and current configuration is not automatically incorrect.",
                finding),
            IdentityExposureFindingType.HighRiskCapabilityActivelyObserved => New(
                IdentityExposureRecommendationType.ReviewActiveHighRiskGovernancePath,
                "Review active high-risk governance path",
                $"This {finding.CapabilityRisk} capability is actively exercised. Its risk classification is curated capability metadata, not a finding score.",
                "Review its governing policies and recent evidence to confirm the governance path remains intentional and has appropriate controls.",
                "Active use is not inherently adverse and does not establish business necessity.",
                finding),
            IdentityExposureFindingType.MultiplePoliciesContribute => New(
                IdentityExposureRecommendationType.ReviewMultiplePolicyContext,
                "Review combined policy context",
                "Multiple policies contribute to this capability's current governance context.",
                "Review the contributing policies together to confirm the combined governance intent is clear.",
                "Multiple policy relationships do not by themselves establish a conflict or require consolidation.",
                finding),
            _ => throw new ArgumentOutOfRangeException(nameof(finding),
                finding.FindingType, "Unsupported exposure finding type.")
        };

    private static IdentityExposureRecommendation New(
        IdentityExposureRecommendationType type, string title, string explanation,
        string suggestedNextStep, string evidenceLimitations,
        IdentityExposureFinding finding) => new(type, title, explanation,
            suggestedNextStep, evidenceLimitations, finding.FindingType,
            finding.IdentityId, finding.CapabilityId, finding.Technology,
            finding.CapabilityRisk, finding.EvidenceCoverageStatus,
            finding.WindowStartUtc, finding.WindowEndUtc, finding.SupportingFacts,
            finding.RelevantPolicies, finding.CapabilityProvenance,
            finding.CurrentConfigurationFingerprint,
            finding.ObservedConfigurationFingerprints,
            finding.ObservationsWithoutConfigurationProvenance);

    private static int TypeOrder(IdentityExposureRecommendationType type) => type switch
    {
        IdentityExposureRecommendationType.ReviewHistoricalActivityAgainstCurrentGovernance => 0,
        IdentityExposureRecommendationType.ReviewCurrentGovernanceRelationship => 1,
        IdentityExposureRecommendationType.ReviewHistoricalConfigurationChanges => 2,
        IdentityExposureRecommendationType.ReviewActiveHighRiskGovernancePath => 3,
        IdentityExposureRecommendationType.ReviewMultiplePolicyContext => 4,
        _ => 5
    };

    private static int RiskOrder(string risk) => risk switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        _ => 4
    };
}

public enum IdentityExposureRecommendationType
{
    ReviewHistoricalActivityAgainstCurrentGovernance,
    ReviewCurrentGovernanceRelationship,
    ReviewHistoricalConfigurationChanges,
    ReviewActiveHighRiskGovernancePath,
    ReviewMultiplePolicyContext
}

public sealed record IdentityExposureRecommendation(
    IdentityExposureRecommendationType RecommendationType,
    string Title,
    string Explanation,
    string SuggestedNextStep,
    string EvidenceLimitations,
    IdentityExposureFindingType SourceFindingType,
    string IdentityId,
    string CapabilityId,
    string Technology,
    string CapabilityRisk,
    IdentityEvidenceCoverageStatus EvidenceCoverageStatus,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    IReadOnlyCollection<IdentityExposureFindingFact> SupportingFacts,
    IReadOnlyCollection<string> RelevantPolicies,
    string CapabilityProvenance,
    string? CurrentConfigurationFingerprint,
    IReadOnlyCollection<string> ObservedConfigurationFingerprints,
    int ObservationsWithoutConfigurationProvenance);
