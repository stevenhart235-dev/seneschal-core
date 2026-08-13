using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Services;

public sealed class IdentityExposureAnalysisService
{
    public const int DefaultObservationDays = 30;
    private readonly OperatorGovernanceContextService _governanceContext;
    private readonly ICapabilityCatalog _capabilities;
    private readonly IAuditEventStore _audit;

    public IdentityExposureAnalysisService(
        OperatorGovernanceContextService governanceContext,
        ICapabilityCatalog capabilities,
        IAuditEventStore audit)
    {
        _governanceContext = governanceContext;
        _capabilities = capabilities;
        _audit = audit;
    }

    public async Task<IdentityExposureAnalysis> AnalyzeAsync(
        IdentityExposureQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.IdentityId);
        if (query.WindowStartUtc > query.WindowEndUtc)
            throw new ArgumentException("Observation window start must not follow its end.");

        var configured = await _governanceContext.GetIdentityCapabilitiesAsync(
            query.IdentityId, cancellationToken);
        var evidence = (await _audit.GetRecentAsync(int.MaxValue, cancellationToken))
            .Where(item => string.Equals(item.IdentityId, query.IdentityId,
                StringComparison.OrdinalIgnoreCase))
            .Where(item => item.TimestampUtc >= query.WindowStartUtc &&
                item.TimestampUtc <= query.WindowEndUtc)
            .Where(item => item.ApprovalAction is not ("Approved" or "Rejected"))
            .ToList();

        var configuredByCapability = configured.GroupBy(item => item.CapabilityId,
            StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key,
            StringComparer.OrdinalIgnoreCase);
        var observedByCapability = evidence.GroupBy(item => item.CapabilityId,
            StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key,
            StringComparer.OrdinalIgnoreCase);
        var ids = configuredByCapability.Keys.Concat(observedByCapability.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var items = new List<IdentityExposureItem>();

        foreach (var id in ids)
        {
            configuredByCapability.TryGetValue(id, out var configuredGroup);
            observedByCapability.TryGetValue(id, out var observedGroup);
            var configuredItems = configuredGroup?.ToList() ?? [];
            var observedItems = observedGroup?.ToList() ?? [];
            var entry = await _capabilities.GetByIdAsync(id, cancellationToken);
            var first = configuredItems.FirstOrDefault();
            var isConfigured = configuredItems.Count > 0;
            var isObserved = observedItems.Count > 0;
            items.Add(new IdentityExposureItem(
                id,
                entry?.Capability.DisplayName ?? first?.DisplayName ?? id,
                entry?.Capability.Technology ?? first?.Technology ?? string.Empty,
                entry?.Capability.Category ?? first?.Category ?? string.Empty,
                entry?.Capability.RiskLevel.ToString() ?? first?.Risk ?? "Unknown",
                OperatorGovernanceContextService.FormatProvenance(
                    entry?.Provenance ?? []),
                configuredItems.Select(item => item.PolicyName).Distinct(
                    StringComparer.OrdinalIgnoreCase).OrderBy(value => value,
                    StringComparer.OrdinalIgnoreCase).ToList(),
                configuredItems.Select(item => item.Decision).Distinct(
                    StringComparer.OrdinalIgnoreCase).OrderBy(value => value,
                    StringComparer.OrdinalIgnoreCase).ToList(),
                configuredItems.SelectMany(item => item.Environments).Distinct(
                    StringComparer.OrdinalIgnoreCase).OrderBy(value => value,
                    StringComparer.OrdinalIgnoreCase).ToList(),
                observedItems.Count,
                observedItems.Select(item => (DateTimeOffset?)item.TimestampUtc).Max(),
                State(isConfigured, isObserved)));
        }

        var allItems = items.OrderBy(item => RiskOrder(item.Risk))
            .ThenBy(item => item.CapabilityId, StringComparer.OrdinalIgnoreCase).ToList();
        var filtered = allItems.Where(item => Matches(query.State, item.State) &&
                Matches(query.Risk, item.Risk) &&
                Matches(query.Technology, item.Technology)).ToList();
        return new IdentityExposureAnalysis(query.IdentityId, query.WindowStartUtc,
            query.WindowEndUtc, allItems, filtered,
            BuildSummary(allItems));
    }

    private static IdentityExposureSummary BuildSummary(
        IReadOnlyCollection<IdentityExposureItem> items) => new(
        items.Count(item => item.State is IdentityExposureState.ConfiguredAndObserved or IdentityExposureState.ConfiguredNotObserved),
        items.Count(item => item.ObservedCount > 0),
        items.Count(item => item.State == IdentityExposureState.ConfiguredAndObserved),
        items.Count(item => item.State == IdentityExposureState.ConfiguredNotObserved),
        items.Count(item => item.State == IdentityExposureState.ObservedNotConfigured),
        items.Count(item => item.Risk == "Critical" && item.State is IdentityExposureState.ConfiguredAndObserved or IdentityExposureState.ConfiguredNotObserved),
        items.Count(item => item.Risk == "Critical" && item.State == IdentityExposureState.ConfiguredAndObserved),
        items.Count(item => item.Risk == "Critical" && item.State == IdentityExposureState.ConfiguredNotObserved),
        items.GroupBy(item => string.IsNullOrWhiteSpace(item.Technology) ? "Not specified" : item.Technology,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new IdentityExposureTechnologySummary(group.Key,
                group.Count(item => item.State is IdentityExposureState.ConfiguredAndObserved or IdentityExposureState.ConfiguredNotObserved),
                group.Count(item => item.ObservedCount > 0))).ToList());

    private static IdentityExposureState State(bool configured, bool observed) =>
        (configured, observed) switch
        {
            (true, true) => IdentityExposureState.ConfiguredAndObserved,
            (true, false) => IdentityExposureState.ConfiguredNotObserved,
            _ => IdentityExposureState.ObservedNotConfigured
        };

    private static bool Matches(string? filter, object value) =>
        string.IsNullOrWhiteSpace(filter) || string.Equals(filter, value.ToString(),
            StringComparison.OrdinalIgnoreCase);

    private static int RiskOrder(string risk) => risk.ToLowerInvariant() switch
    {
        "critical" => 0, "high" => 1, "medium" => 2, "low" => 3, _ => 4
    };
}

public sealed record IdentityExposureQuery(string IdentityId,
    DateTimeOffset WindowStartUtc, DateTimeOffset WindowEndUtc,
    string? State = null, string? Risk = null, string? Technology = null);
public enum IdentityExposureState { ConfiguredAndObserved, ConfiguredNotObserved, ObservedNotConfigured }
public sealed record IdentityExposureItem(string CapabilityId, string DisplayName,
    string Technology, string Category, string Risk, string Provenance,
    IReadOnlyCollection<string> Policies, IReadOnlyCollection<string> Decisions,
    IReadOnlyCollection<string> Environments, int ObservedCount,
    DateTimeOffset? MostRecentObservedUtc, IdentityExposureState State);
public sealed record IdentityExposureTechnologySummary(string Technology,
    int ConfiguredCount, int ObservedCount);
public sealed record IdentityExposureSummary(int ConfiguredCount, int ObservedCount,
    int ConfiguredAndObservedCount, int ConfiguredNotObservedCount,
    int ObservedNotConfiguredCount, int CriticalConfiguredCount,
    int CriticalObservedCount, int CriticalNotObservedCount,
    IReadOnlyCollection<IdentityExposureTechnologySummary> Technologies);
public sealed record IdentityExposureAnalysis(string IdentityId,
    DateTimeOffset WindowStartUtc, DateTimeOffset WindowEndUtc,
    IReadOnlyCollection<IdentityExposureItem> AllItems,
    IReadOnlyCollection<IdentityExposureItem> Items,
    IdentityExposureSummary Summary);