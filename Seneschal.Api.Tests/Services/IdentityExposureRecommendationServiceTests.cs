using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class IdentityExposureRecommendationServiceTests
{
    private readonly IdentityExposureRecommendationService _service = new();
    private readonly DateTimeOffset _start = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _end = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(IdentityEvidenceCoverageStatus.Full, "fully covered selected period")]
    [InlineData(IdentityEvidenceCoverageStatus.Partial, "does not cover the complete requested period")]
    public void NotObservedRecommendation_QualifiesEvidenceCoverage(
        IdentityEvidenceCoverageStatus coverage, string expected)
    {
        var recommendation = Assert.Single(_service.Generate([
            Finding(IdentityExposureFindingType.HighRiskConfiguredNotObserved,
                coverage: coverage)]));

        Assert.Equal(IdentityExposureRecommendationType.ReviewCurrentGovernanceRelationship,
            recommendation.RecommendationType);
        Assert.Contains(expected, recommendation.Explanation,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(coverage, recommendation.EvidenceCoverageStatus);
    }

    [Fact]
    public void UnknownCoverageWithoutSourceFinding_ProducesNoRecommendation()
    {
        var findingService = new IdentityExposureFindingService();
        var item = new IdentityExposureItem("postgres.restore.execute", "Restore database",
            "PostgreSQL", "recovery", "Critical", "Pack: postgres 1.0.0",
            ["restore-policy"], ["RequireApproval"], ["prod"], 0, null,
            IdentityExposureState.ConfiguredNotObserved,
            new Dictionary<string, int>(), 0);
        var analysis = new IdentityExposureAnalysis("operator", _start, _end,
            [item], [item], new IdentityExposureSummary(1, 0, 0, 1, 0, 1, 0, 1, []),
            new IdentityExposureCoverage(IdentityEvidenceCoverageStatus.Unknown, null,
                "Coverage unavailable"),
            new IdentityExposureConfigurationProvenance("sha256:current", [], 0));

        Assert.Empty(_service.Generate(findingService.Generate(analysis)));
    }

    [Fact]
    public void OutsideContextRecommendation_RetainsHistoricalFingerprintContext()
    {
        var recommendation = Assert.Single(_service.Generate([
            Finding(IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext,
                fingerprints: ["sha256:historical"]) ]));

        Assert.Equal(IdentityExposureRecommendationType.ReviewHistoricalActivityAgainstCurrentGovernance,
            recommendation.RecommendationType);
        Assert.Equal(["sha256:historical"], recommendation.ObservedConfigurationFingerprints);
        Assert.Contains("intentionally differs", recommendation.SuggestedNextStep,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("add a policy", recommendation.SuggestedNextStep,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoricalDifferenceRecommendation_PreservesLimitations()
    {
        var recommendation = Assert.Single(_service.Generate([
            Finding(IdentityExposureFindingType.HistoricalConfigurationDiffers,
                fingerprints: ["sha256:historical"]) ]));

        Assert.Contains("do not identify a specific changed policy",
            recommendation.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not automatically invalid", recommendation.EvidenceLimitations,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("caused", recommendation.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public void ActiveHighRiskRecommendation_UsesNeutralReviewLanguage(string risk)
    {
        var recommendation = Assert.Single(_service.Generate([
            Finding(IdentityExposureFindingType.HighRiskCapabilityActivelyObserved,
                risk: risk)]));

        Assert.Equal(IdentityExposureRecommendationType.ReviewActiveHighRiskGovernancePath,
            recommendation.RecommendationType);
        Assert.Contains("review", recommendation.SuggestedNextStep,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not inherently adverse", recommendation.EvidenceLimitations,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultiplePoliciesRecommendation_RetainsReferencesWithoutConflictClaim()
    {
        var recommendation = Assert.Single(_service.Generate([
            Finding(IdentityExposureFindingType.MultiplePoliciesContribute,
                policies: ["policy-a", "policy-b"]) ]));

        Assert.Equal(["policy-a", "policy-b"], recommendation.RelevantPolicies);
        Assert.Contains("do not by themselves establish a conflict",
            recommendation.EvidenceLimitations, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conflicting", recommendation.Explanation,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryRecommendationRetainsSourceFindingAndEvidence()
    {
        var findings = Enum.GetValues<IdentityExposureFindingType>()
            .Select((type, index) => Finding(type, id: $"capability-{index}"))
            .ToList();

        var recommendations = _service.Generate(findings);

        Assert.Equal(findings.Count, recommendations.Count);
        Assert.All(recommendations, recommendation =>
        {
            Assert.Contains(findings, finding =>
                finding.FindingType == recommendation.SourceFindingType &&
                finding.IdentityId == recommendation.IdentityId &&
                finding.CapabilityId == recommendation.CapabilityId);
            Assert.NotEmpty(recommendation.SupportingFacts);
            Assert.Equal(_start, recommendation.WindowStartUtc);
            Assert.Equal(_end, recommendation.WindowEndUtc);
            Assert.Equal("Pack: postgres 1.0.0", recommendation.CapabilityProvenance);
        });
    }

    [Fact]
    public void NoFindingProducesNoRecommendation() =>
        Assert.Empty(_service.Generate([]));

    [Fact]
    public void DuplicateSourceFindingProducesAtMostOneRecommendation()
    {
        var finding = Finding(
            IdentityExposureFindingType.HighRiskConfiguredNotObserved);

        Assert.Single(_service.Generate([finding, finding]));
    }

    [Fact]
    public void RecommendationsAreDeterministicallyOrdered()
    {
        var findings = new[]
        {
            Finding(IdentityExposureFindingType.MultiplePoliciesContribute,
                id: "complex", risk: "Low"),
            Finding(IdentityExposureFindingType.HighRiskConfiguredNotObserved,
                id: "a-high", risk: "High"),
            Finding(IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext,
                id: "outside", risk: "Low"),
            Finding(IdentityExposureFindingType.HighRiskConfiguredNotObserved,
                id: "z-critical", risk: "Critical")
        };

        var first = _service.Generate(findings);
        var second = _service.Generate(findings);

        Assert.Equal(first.Select(ValueProjection), second.Select(ValueProjection));
        Assert.Equal(["outside", "z-critical", "a-high", "complex"],
            first.Select(item => item.CapabilityId));
    }

    [Fact]
    public void RecommendationsContainNoUnsupportedLanguage()
    {
        var recommendations = _service.Generate(Enum.GetValues<IdentityExposureFindingType>()
            .Select((type, index) => Finding(type, id: $"capability-{index}")));
        var text = string.Join(" ", recommendations.SelectMany(item => new[]
        {
            item.Title, item.Explanation, item.SuggestedNextStep,
            item.EvidenceLimitations
        }));

        foreach (var phrase in new[] { "safe to remove", "remove this capability",
            "revoke this permission", "overprivileged", "unauthorized", "must change",
            "automatically apply" })
            Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
    }

    private IdentityExposureFinding Finding(IdentityExposureFindingType type,
        string id = "postgres.restore.execute", string risk = "Critical",
        IdentityEvidenceCoverageStatus coverage = IdentityEvidenceCoverageStatus.Full,
        IReadOnlyCollection<string>? policies = null,
        IReadOnlyCollection<string>? fingerprints = null) => new(type,
            type.ToString(), "Source explanation", "Source limitation", "operator",
            id, "PostgreSQL", risk, "Pack: postgres 1.0.0", coverage,
            _start, _end, [new IdentityExposureFindingFact("Observation count", "1")],
            policies ?? ["policy-a"], "sha256:current", fingerprints ?? [], 0);

    private static string ValueProjection(IdentityExposureRecommendation item) =>
        $"{item.RecommendationType}|{item.SourceFindingType}|{item.IdentityId}|{item.CapabilityId}|{item.Title}|{item.Explanation}|{item.SuggestedNextStep}";
}
