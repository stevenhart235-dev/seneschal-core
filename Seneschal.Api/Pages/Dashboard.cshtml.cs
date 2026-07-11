using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class DashboardModel : PageModel
{
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly IGovernanceGraph _governanceGraph;
    private readonly IActivityStore _activityStore;
    private readonly IAuditEventStore _auditEventStore;
    private readonly IdentityLoader _identityLoader;
    private readonly PolicyLoader _policyLoader;
    private readonly IGovernanceModeStore _governanceModeStore;

    public static readonly TimeSpan ActiveThreshold = TimeSpan.FromSeconds(20);

    public DashboardModel(
        ICapabilityCatalog capabilityCatalog,
        IGovernanceGraph governanceGraph,
        IActivityStore activityStore,
        IAuditEventStore auditEventStore,
        IdentityLoader identityLoader,
        PolicyLoader policyLoader,
        IGovernanceModeStore governanceModeStore)
    {
        _capabilityCatalog = capabilityCatalog;
        _governanceGraph = governanceGraph;
        _activityStore = activityStore;
        _auditEventStore = auditEventStore;
        _identityLoader = identityLoader;
        _policyLoader = policyLoader;
        _governanceModeStore = governanceModeStore;
    }

    public int TotalCapabilities { get; private set; }
    public int TotalPolicies { get; private set; }
    public int TotalIdentities { get; private set; }
    public int TotalRelationships { get; private set; }

    public IReadOnlyCollection<CapabilityCatalogEntry> HighestRiskCapabilities
        { get; private set; } = [];
    public IReadOnlyCollection<CapabilityCatalogEntry> RecentlyAddedCapabilities
        { get; private set; } = [];
    public ActivitySnapshot Activity { get; private set; } = new();
    public long TotalRuntimeDecisions { get; private set; }
    public long AllowedRuntimeDecisions { get; private set; }
    public long DeniedRuntimeDecisions { get; private set; }
    public long PendingApprovalRuntimeDecisions { get; private set; }
    public double AverageEvaluationDurationMs { get; private set; }
    public CapabilityActivity? MostActiveCapability { get; private set; }
    public CapabilityActivity? MostDeniedCapability { get; private set; }
    public IdentityActivity? MostActiveIdentity { get; private set; }
    public PolicyActivity? MostMatchedPolicy { get; private set; }
    public IReadOnlyCollection<CapabilityActivity> TopCapabilitiesByRequestCount
        { get; private set; } = [];
    public IReadOnlyCollection<CapabilityActivity> MostDeniedCapabilities
        { get; private set; } = [];
    public IReadOnlyCollection<IdentityActivity> MostActiveIdentities
        { get; private set; } = [];
    public bool AuditEventsAvailable { get; private set; }
    public DashboardLiveSnapshot Live { get; private set; } = DashboardLiveSnapshot.Empty;

    public bool HasRuntimeActivity => TotalRuntimeDecisions > 0;
    public bool ShowFirstRunExperience => TotalRuntimeDecisions < 3;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var capabilities = await _capabilityCatalog.SearchAsync(
            new CapabilityCatalogQuery(),
            cancellationToken);
        var relationships = await _governanceGraph.QueryAsync(
            new GovernanceRelationshipQuery(),
            cancellationToken);
        Activity = await _activityStore.GetSnapshotAsync(cancellationToken);
        var auditEvents = await _auditEventStore.GetRecentAsync(
            count: 100,
            cancellationToken);

        TotalCapabilities = capabilities.Count;
        TotalPolicies = _policyLoader.GetPolicies().Count;
        TotalIdentities = _identityLoader.GetIdentities().Count;
        TotalRelationships = relationships.Count;
        AuditEventsAvailable = auditEvents.Count > 0;
        Live = CreateLiveSnapshot(
            auditEvents,
            _governanceModeStore.GetMode(),
            DateTimeOffset.UtcNow);
        TotalRuntimeDecisions = Activity.Capabilities.Sum(
            capability => capability.TotalRequests);
        AllowedRuntimeDecisions = Activity.Capabilities.Sum(
            capability => capability.AllowedCount);
        DeniedRuntimeDecisions = Activity.Capabilities.Sum(
            capability => capability.DeniedCount);
        PendingApprovalRuntimeDecisions = Activity.Capabilities.Sum(
            capability => capability.PendingApprovalCount);
        AverageEvaluationDurationMs = TotalRuntimeDecisions == 0
            ? 0
            : Activity.Capabilities.Sum(capability =>
                capability.AverageEvaluationDurationMs *
                capability.TotalRequests) / TotalRuntimeDecisions;

        MostActiveCapability = Activity.Capabilities
            .OrderByDescending(capability => capability.TotalRequests)
            .ThenBy(capability => capability.CapabilityId)
            .FirstOrDefault();
        MostDeniedCapability = Activity.Capabilities
            .Where(capability => capability.DeniedCount > 0)
            .OrderByDescending(capability => capability.DeniedCount)
            .ThenBy(capability => capability.CapabilityId)
            .FirstOrDefault();
        MostActiveIdentity = Activity.Identities
            .OrderByDescending(identity => identity.TotalRequests)
            .ThenBy(identity => identity.IdentityId)
            .FirstOrDefault();
        MostMatchedPolicy = Activity.Policies
            .OrderByDescending(policy => policy.MatchCount)
            .ThenBy(policy => policy.PolicyId)
            .FirstOrDefault();
        TopCapabilitiesByRequestCount = Activity.Capabilities
            .OrderByDescending(capability => capability.TotalRequests)
            .ThenBy(capability => capability.CapabilityId)
            .Take(5)
            .ToList();
        MostDeniedCapabilities = Activity.Capabilities
            .Where(capability => capability.DeniedCount > 0)
            .OrderByDescending(capability => capability.DeniedCount)
            .ThenBy(capability => capability.CapabilityId)
            .Take(5)
            .ToList();
        MostActiveIdentities = Activity.Identities
            .OrderByDescending(identity => identity.TotalRequests)
            .ThenBy(identity => identity.IdentityId)
            .Take(5)
            .ToList();

        HighestRiskCapabilities = capabilities
            .OrderByDescending(entry => entry.Capability.RiskLevel)
            .ThenBy(entry => entry.Capability.Id)
            .Take(5)
            .ToList();

        RecentlyAddedCapabilities = capabilities
            .Reverse()
            .Take(5)
            .ToList();
    }

    public async Task<JsonResult> OnGetLiveAsync(
        CancellationToken cancellationToken)
    {
        var auditEvents = await _auditEventStore.GetRecentAsync(
            count: 100,
            cancellationToken);
        var activity = await _activityStore.GetSnapshotAsync(cancellationToken);
        var snapshot = CreateLiveSnapshot(
            auditEvents,
            _governanceModeStore.GetMode(),
            DateTimeOffset.UtcNow) with
        {
            TotalDecisions = activity.Capabilities.Sum(
                capability => capability.TotalRequests),
            Allowed = activity.Capabilities.Sum(
                capability => capability.AllowedCount),
            Denied = activity.Capabilities.Sum(
                capability => capability.DeniedCount),
            Pending = activity.Capabilities.Sum(
                capability => capability.PendingApprovalCount)
        };

        return new JsonResult(snapshot);
    }

    public static DashboardLiveSnapshot CreateLiveSnapshot(
        IEnumerable<AuditEvent> auditEvents,
        EnforcementMode currentMode,
        DateTimeOffset now)
    {
        var events = auditEvents
            .OrderByDescending(auditEvent => auditEvent.TimestampUtc)
            .ToList();
        var activeAfter = now - ActiveThreshold;
        var recentEvents = events
            .Where(auditEvent => auditEvent.TimestampUtc >= activeAfter)
            .ToList();
        var decisions = events
            .Take(10)
            .Select(ToLiveDecision)
            .ToList();
        var identities = events
            .GroupBy(auditEvent => auditEvent.IdentityId)
            .Select(group => group.First())
            .OrderByDescending(auditEvent => auditEvent.TimestampUtc)
            .Select(auditEvent => new DashboardActiveIdentity(
                auditEvent.IdentityId,
                auditEvent.CapabilityId,
                DecisionLabel(auditEvent.Decision),
                auditEvent.TimestampUtc,
                auditEvent.TimestampUtc >= activeAfter ? "Live" : "Idle"))
            .ToList();

        return new DashboardLiveSnapshot(
            currentMode.ToString(),
            events.Count,
            events.Count(auditEvent => auditEvent.Decision == DecisionType.Allow),
            events.Count(auditEvent => auditEvent.Decision == DecisionType.Deny),
            events.Count(auditEvent => auditEvent.Decision == DecisionType.RequireApproval),
            recentEvents.Select(auditEvent => auditEvent.IdentityId).Distinct().Count(),
            recentEvents.Select(auditEvent => auditEvent.CapabilityId).Distinct().Count(),
            events.FirstOrDefault()?.TimestampUtc,
            now,
            decisions,
            identities);
    }

    private static DashboardLiveDecision ToLiveDecision(AuditEvent auditEvent)
    {
        var decision = DecisionLabel(auditEvent.Decision);
        var effectiveAction = auditEvent.Decision switch
        {
            DecisionType.Allow => "Executed",
            DecisionType.RequireApproval
                when auditEvent.EnforcementMode == EnforcementMode.Enforce
                => "Blocked pending approval",
            _ when auditEvent.EnforcementMode == EnforcementMode.Enforce
                => "Blocked",
            _ => "Executed and recorded"
        };

        return new DashboardLiveDecision(
            auditEvent.Id,
            auditEvent.TimestampUtc,
            auditEvent.IdentityId,
            auditEvent.CapabilityId,
            decision,
            auditEvent.EnforcementMode.ToString(),
            effectiveAction,
            auditEvent.Reason,
            auditEvent.Environment,
            auditEvent.MatchedPolicies.FirstOrDefault());
    }

    private static string DecisionLabel(DecisionType decision) =>
        decision == DecisionType.RequireApproval ? "PendingApproval" : decision.ToString();
}

public sealed record DashboardLiveSnapshot(
    string CurrentMode,
    long TotalDecisions,
    long Allowed,
    long Denied,
    long Pending,
    int ActiveIdentityCount,
    int ActiveCapabilityCount,
    DateTimeOffset? LastEvaluationUtc,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyCollection<DashboardLiveDecision> Decisions,
    IReadOnlyCollection<DashboardActiveIdentity> Identities)
{
    public static DashboardLiveSnapshot Empty { get; } = new(
        "LogOnly", 0, 0, 0, 0, 0, 0, null, DateTimeOffset.UtcNow, [], []);
}

public sealed record DashboardLiveDecision(
    string Id,
    DateTimeOffset TimestampUtc,
    string Identity,
    string Capability,
    string Decision,
    string Mode,
    string EffectiveAction,
    string Reason,
    string Environment,
    string? MatchedPolicy);

public sealed record DashboardActiveIdentity(
    string Identity,
    string LatestCapability,
    string LatestDecision,
    DateTimeOffset LastSeenUtc,
    string Status);
