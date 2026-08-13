namespace Seneschal.Api.Services;

public sealed class IdentityExposureFindingService
{
    public IReadOnlyCollection<IdentityExposureFinding> Generate(
        IdentityExposureAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var findings = new List<IdentityExposureFinding>();
        foreach (var item in analysis.AllItems)
        {
            if (item.State == IdentityExposureState.ObservedNotConfigured)
                findings.Add(CreateOutsideContext(analysis, item));

            if (item.State == IdentityExposureState.ConfiguredNotObserved &&
                IsHighRisk(item.Risk) &&
                analysis.Coverage.Status != IdentityEvidenceCoverageStatus.Unknown)
                findings.Add(CreateHighRiskNotObserved(analysis, item));

            var differing = item.ObservationsByConfigurationFingerprint
                .Where(entry => analysis.ConfigurationProvenance.CurrentFingerprint is not null &&
                    !string.Equals(entry.Key,
                        analysis.ConfigurationProvenance.CurrentFingerprint,
                        StringComparison.Ordinal))
                .OrderBy(entry => entry.Key, StringComparer.Ordinal).ToList();
            if (differing.Count > 0)
                findings.Add(CreateConfigurationDiffers(analysis, item, differing));

            if (item.ObservedCount > 0 && IsHighRisk(item.Risk))
                findings.Add(CreateHighRiskObserved(analysis, item));

            if (item.Policies.Count > 1)
                findings.Add(CreateMultiplePolicies(analysis, item));
        }

        return findings.OrderBy(item => TypeOrder(item.FindingType))
            .ThenBy(item => RiskOrder(item.CapabilityRisk))
            .ThenBy(item => item.CapabilityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FindingType).ToList();
    }

    private static IdentityExposureFinding CreateOutsideContext(
        IdentityExposureAnalysis analysis, IdentityExposureItem item) => New(
        IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext,
        "Observed outside current configured governance context",
        $"{item.DisplayName} was observed {item.ObservedCount} time(s) in retained evidence, but no current static policy target relationship represents this capability for the identity.",
        "This does not prove unauthorized execution. Current configuration may differ from historical configuration, or other evidence/configuration conditions may apply.",
        analysis, item,
        Facts(("Observation count", item.ObservedCount.ToString()),
            ("Most recent observation", Timestamp(item.MostRecentObservedUtc)),
            ("Current configured policies", "0"),
            ("Configuration provenance unavailable", item.ObservationsWithoutConfigurationProvenance.ToString())));

    private static IdentityExposureFinding CreateHighRiskNotObserved(
        IdentityExposureAnalysis analysis, IdentityExposureItem item)
    {
        var full = analysis.Coverage.Status == IdentityEvidenceCoverageStatus.Full;
        return New(IdentityExposureFindingType.HighRiskConfiguredNotObserved,
            $"{item.Risk} capability with no observed use" +
                (full ? string.Empty : " found in retained evidence"),
            full
                ? $"{item.DisplayName} is represented by current configured governance context, and no activity was observed during the fully covered selected period."
                : $"{item.DisplayName} is represented by current configured governance context, and no use was found in retained evidence. Coverage is partial, so absence across the complete requested period cannot be determined.",
            "This does not establish business necessity or justify changing current governance configuration.",
            analysis, item,
            Facts(("Configured policies", item.Policies.Count.ToString()),
                ("Configured decisions", Join(item.Decisions)),
                ("Configured environments", Join(item.Environments)),
                ("Observed evaluations", "0")));
    }

    private static IdentityExposureFinding CreateConfigurationDiffers(
        IdentityExposureAnalysis analysis, IdentityExposureItem item,
        IReadOnlyCollection<KeyValuePair<string, int>> differing) => New(
        IdentityExposureFindingType.HistoricalConfigurationDiffers,
        "Historical evaluation configuration differs from current",
        $"{differing.Sum(entry => entry.Value)} observation(s) for {item.DisplayName} were recorded under evaluation-relevant governance configuration different from the current fingerprint.",
        "This does not identify a specific policy change, prove policy drift caused a decision, or prove the result would now differ.",
        analysis, item,
        Facts(("Current fingerprint", analysis.ConfigurationProvenance.CurrentFingerprint ?? "Unavailable"),
            ("Different historical fingerprints", string.Join(", ", differing.Select(entry => $"{entry.Key} ({entry.Value})"))),
            ("Affected observations", differing.Sum(entry => entry.Value).ToString())));

    private static IdentityExposureFinding CreateHighRiskObserved(
        IdentityExposureAnalysis analysis, IdentityExposureItem item) => New(
        IdentityExposureFindingType.HighRiskCapabilityActivelyObserved,
        $"{item.Risk} capability actively observed",
        $"{item.DisplayName} was observed {item.ObservedCount} time(s) during the selected period. Its {item.Risk} classification is curated capability metadata.",
        "Active observation is not inherently adverse and does not establish business necessity or a risk score.",
        analysis, item,
        Facts(("Observation count", item.ObservedCount.ToString()),
            ("Most recent observation", Timestamp(item.MostRecentObservedUtc)),
            ("Configured governance context", item.Policies.Count > 0 ? $"{item.Policies.Count} policy/policies" : "None in current context")));

    private static IdentityExposureFinding CreateMultiplePolicies(
        IdentityExposureAnalysis analysis, IdentityExposureItem item) => New(
        IdentityExposureFindingType.MultiplePoliciesContribute,
        "Multiple policies contribute to capability context",
        $"{item.Policies.Count} current configured policies contribute governance context for {item.DisplayName}.",
        "Multiple contributing policies do not by themselves prove a conflict.",
        analysis, item,
        Facts(("Policies", Join(item.Policies)),
            ("Decisions", Join(item.Decisions)),
            ("Environments", Join(item.Environments))));

    private static IdentityExposureFinding New(IdentityExposureFindingType type,
        string title, string explanation, string doesNotProve,
        IdentityExposureAnalysis analysis, IdentityExposureItem item,
        IReadOnlyCollection<IdentityExposureFindingFact> facts) => new(
            type, title, explanation, doesNotProve, analysis.IdentityId,
            item.CapabilityId, item.Technology, item.Risk, item.Provenance,
            analysis.Coverage.Status, analysis.WindowStartUtc, analysis.WindowEndUtc,
            facts, analysis.ConfigurationProvenance.CurrentFingerprint,
            item.ObservationsByConfigurationFingerprint.Keys.OrderBy(value => value,
                StringComparer.Ordinal).ToList(),
            item.ObservationsWithoutConfigurationProvenance);

    private static IReadOnlyCollection<IdentityExposureFindingFact> Facts(
        params (string Label, string Value)[] facts) =>
        facts.Select(fact => new IdentityExposureFindingFact(fact.Label, fact.Value)).ToList();
    private static bool IsHighRisk(string risk) => risk is "Critical" or "High";
    private static string Timestamp(DateTimeOffset? value) => value?.ToString("u") ?? "Not recorded";
    private static string Join(IEnumerable<string> values) =>
        string.Join(", ", values.DefaultIfEmpty("None"));
    private static int TypeOrder(IdentityExposureFindingType type) => type switch
    {
        IdentityExposureFindingType.ObservedOutsideCurrentGovernanceContext => 0,
        IdentityExposureFindingType.HighRiskConfiguredNotObserved => 1,
        IdentityExposureFindingType.HistoricalConfigurationDiffers => 2,
        IdentityExposureFindingType.HighRiskCapabilityActivelyObserved => 3,
        IdentityExposureFindingType.MultiplePoliciesContribute => 4,
        _ => 5
    };
    private static int RiskOrder(string risk) => risk switch
    { "Critical" => 0, "High" => 1, "Medium" => 2, "Low" => 3, _ => 4 };
}

public enum IdentityExposureFindingType
{
    ObservedOutsideCurrentGovernanceContext,
    HighRiskConfiguredNotObserved,
    HistoricalConfigurationDiffers,
    HighRiskCapabilityActivelyObserved,
    MultiplePoliciesContribute
}

public sealed record IdentityExposureFinding(
    IdentityExposureFindingType FindingType, string Title, string Explanation,
    string WhatThisDoesNotProve, string IdentityId, string CapabilityId,
    string Technology, string CapabilityRisk, string CapabilityProvenance,
    IdentityEvidenceCoverageStatus EvidenceCoverageStatus,
    DateTimeOffset WindowStartUtc, DateTimeOffset WindowEndUtc,
    IReadOnlyCollection<IdentityExposureFindingFact> SupportingFacts,
    string? CurrentConfigurationFingerprint,
    IReadOnlyCollection<string> ObservedConfigurationFingerprints,
    int ObservationsWithoutConfigurationProvenance);
public sealed record IdentityExposureFindingFact(string Label, string Value);
