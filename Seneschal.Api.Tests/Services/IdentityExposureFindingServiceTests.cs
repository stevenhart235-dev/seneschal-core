using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class IdentityExposureFindingServiceTests
{
    private readonly IdentityExposureFindingService _service = new();
    private readonly DateTimeOffset _start = new(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _end = new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(IdentityEvidenceCoverageStatus.Full, "fully covered selected period")]
    [InlineData(IdentityEvidenceCoverageStatus.Partial, "no use was found in retained evidence")]
    public void HighRiskConfiguredNotObserved_QualifiesCoverage(
        IdentityEvidenceCoverageStatus coverage, string expected)
    {
        var finding = Assert.Single(_service.Generate(Analysis(coverage,
            Item("db.restore", "Critical", IdentityExposureState.ConfiguredNotObserved))));
        Assert.Equal(IdentityExposureFindingType.HighRiskConfiguredNotObserved,
            finding.FindingType);
        Assert.Contains(expected, finding.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(coverage, finding.EvidenceCoverageStatus);
        Assert.Equal(_start, finding.WindowStartUtc);
        Assert.Equal(_end, finding.WindowEndUtc);
        Assert.Equal("Pack: postgres 1.0.0", finding.CapabilityProvenance);
    }

    [Fact]
    public void UnknownCoverage_SuppressesAbsenceFinding()
    {
        Assert.Empty(_service.Generate(Analysis(IdentityEvidenceCoverageStatus.Unknown,
            Item("db.restore", "Critical", IdentityExposureState.ConfiguredNotObserved))));
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Medium")]
    public void LowAndMediumNotObserved_DoNotGenerateHighRiskFinding(string risk)
    {
        Assert.Empty(_service.Generate(Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("capability", risk, IdentityExposureState.ConfiguredNotObserved))));
    }

    [Fact]
    public void ObservedCapability_DoesNotGenerateAbsenceFinding()
    {
        var findings = _service.Generate(Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("db.restore", "Critical", IdentityExposureState.ConfiguredAndObserved,
                observed: 2)));
        Assert.DoesNotContain(findings, item =>
            item.FindingType == IdentityExposureFindingType.HighRiskConfiguredNotObserved);
    }

    [Fact]
    public void OutsideContext_IncludesNeutralObservationEvidence()
    {
        var finding = Assert.Single(_service.Generate(Analysis(
            IdentityEvidenceCoverageStatus.Partial,
            Item("github.workflow.run", "Low",
                IdentityExposureState.ObservedNotConfigured, observed: 3))));
        Assert.Equal(IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext,
            finding.FindingType);
        Assert.Contains(finding.SupportingFacts,
            fact => fact.Label == "Observation count" && fact.Value == "3");
        Assert.Contains("does not prove unauthorized execution",
            finding.WhatThisDoesNotProve, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026-08-12", finding.SupportingFacts.Single(
            fact => fact.Label == "Most recent observation").Value);
    }

    [Fact]
    public void HistoricalConfigurationDifference_RequiresDifferentKnownFingerprint()
    {
        var different = _service.Generate(Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("db.read", "Low", IdentityExposureState.ConfiguredAndObserved,
                observed: 2, fingerprints: new Dictionary<string, int>
                { ["sha256:old"] = 2 })));
        var matching = _service.Generate(Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("db.read", "Low", IdentityExposureState.ConfiguredAndObserved,
                observed: 1, fingerprints: new Dictionary<string, int>
                { ["sha256:current"] = 1 })));
        var unavailable = _service.Generate(Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("db.read", "Low", IdentityExposureState.ConfiguredAndObserved,
                observed: 1, unavailable: 1)));

        var finding = Assert.Single(different);
        Assert.Equal(IdentityExposureFindingType.HistoricalConfigurationDiffers,
            finding.FindingType);
        Assert.Contains(finding.SupportingFacts,
            fact => fact.Label == "Affected observations" && fact.Value == "2");
        Assert.Empty(matching);
        Assert.Empty(unavailable);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public void ActiveHighRisk_GeneratesInformationalFact(string risk)
    {
        var finding = Assert.Single(_service.Generate(Analysis(
            IdentityEvidenceCoverageStatus.Full,
            Item("active", risk, IdentityExposureState.ConfiguredAndObserved,
                observed: 4))));
        Assert.Equal(IdentityExposureFindingType.HighRiskCapabilityActivelyObserved,
            finding.FindingType);
        Assert.Contains("not inherently adverse", finding.WhatThisDoesNotProve,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActiveLowRisk_DoesNotGenerateHighRiskActivityFinding()
    {
        Assert.Empty(_service.Generate(Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("active", "Low", IdentityExposureState.ConfiguredAndObserved,
                observed: 1))));
    }

    [Fact]
    public void MultiplePolicies_GeneratesComplexityWithoutConflictClaim()
    {
        var multiple = Item("db.read", "Low",
            IdentityExposureState.ConfiguredNotObserved) with
        { Policies = ["policy-a", "policy-b"] };
        var single = multiple with { Policies = ["policy-a"] };

        var finding = Assert.Single(_service.Generate(Analysis(
            IdentityEvidenceCoverageStatus.Unknown, multiple)));
        Assert.Equal(IdentityExposureFindingType.MultiplePoliciesContribute,
            finding.FindingType);
        Assert.Contains("do not by themselves prove a conflict",
            finding.WhatThisDoesNotProve, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_service.Generate(Analysis(
            IdentityEvidenceCoverageStatus.Unknown, single)));
    }

    [Fact]
    public void Findings_AreDeterministicallyOrderedByTypeRiskAndCapability()
    {
        var analysis = Analysis(IdentityEvidenceCoverageStatus.Full,
            Item("outside", "Low", IdentityExposureState.ObservedNotConfigured, 1),
            Item("z-critical", "Critical", IdentityExposureState.ConfiguredNotObserved),
            Item("a-high", "High", IdentityExposureState.ConfiguredNotObserved),
            Item("active", "Critical", IdentityExposureState.ConfiguredAndObserved, 1),
            Item("complex", "Low", IdentityExposureState.ConfiguredNotObserved) with
                { Policies = ["a", "b"] });

        var first = _service.Generate(analysis);
        var second = _service.Generate(analysis);

        Assert.Equal(
            first.Select(item => (item.FindingType, item.CapabilityId, item.Title,
                item.Explanation, Facts: string.Join("|", item.SupportingFacts))),
            second.Select(item => (item.FindingType, item.CapabilityId, item.Title,
                item.Explanation, Facts: string.Join("|", item.SupportingFacts))));
        Assert.Equal([
            IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext,
            IdentityExposureFindingType.HighRiskConfiguredNotObserved,
            IdentityExposureFindingType.HighRiskConfiguredNotObserved,
            IdentityExposureFindingType.HighRiskCapabilityActivelyObserved,
            IdentityExposureFindingType.MultiplePoliciesContribute],
            first.Select(item => item.FindingType));
        Assert.Equal("z-critical", first.ElementAt(1).CapabilityId);
        Assert.Equal("a-high", first.ElementAt(2).CapabilityId);
    }

    [Fact]
    public void Findings_ContainNoRecommendationOrUnsupportedClaims()
    {
        var text = string.Join(" ", _service.Generate(Analysis(
            IdentityEvidenceCoverageStatus.Full,
            Item("outside", "Critical", IdentityExposureState.ObservedNotConfigured, 1),
            Item("absent", "Critical", IdentityExposureState.ConfiguredNotObserved)))
            .SelectMany(item => new[] { item.Title, item.Explanation,
                item.WhatThisDoesNotProve }));
        Assert.DoesNotContain("safe to remove", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unnecessary", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("overprivileged", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unauthorized capability", text, StringComparison.OrdinalIgnoreCase);
    }

    private IdentityExposureAnalysis Analysis(
        IdentityEvidenceCoverageStatus coverage,
        params IdentityExposureItem[] items) => new("operator", _start, _end,
            items, items, new IdentityExposureSummary(0, 0, 0, 0, 0, 0, 0, 0, []),
            new IdentityExposureCoverage(coverage,
                coverage == IdentityEvidenceCoverageStatus.Unknown ? null : _start,
                "test coverage"),
            new IdentityExposureConfigurationProvenance("sha256:current",
                items.SelectMany(item => item.ObservationsByConfigurationFingerprint.Keys)
                    .Distinct().ToList(),
                items.Sum(item => item.ObservationsWithoutConfigurationProvenance)));

    private IdentityExposureItem Item(string id, string risk,
        IdentityExposureState state, int observed = 0,
        IReadOnlyDictionary<string, int>? fingerprints = null,
        int unavailable = 0) => new(id, id, Technology(id), "operations", risk,
            "Pack: postgres 1.0.0", state == IdentityExposureState.ObservedNotConfigured
                ? [] : ["policy-a"], state == IdentityExposureState.ObservedNotConfigured
                ? [] : ["Allow"], state == IdentityExposureState.ObservedNotConfigured
                ? [] : ["prod"], observed,
            observed > 0 ? _end.AddDays(-1) : null, state,
            fingerprints ?? new Dictionary<string, int>(), unavailable);

    private static string Technology(string id) => id.StartsWith("github")
        ? "github-actions" : "postgresql";
}
