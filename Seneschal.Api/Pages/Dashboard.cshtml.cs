using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
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

    public DashboardModel(
        ICapabilityCatalog capabilityCatalog,
        IGovernanceGraph governanceGraph,
        IActivityStore activityStore,
        IAuditEventStore auditEventStore,
        IdentityLoader identityLoader,
        PolicyLoader policyLoader)
    {
        _capabilityCatalog = capabilityCatalog;
        _governanceGraph = governanceGraph;
        _activityStore = activityStore;
        _auditEventStore = auditEventStore;
        _identityLoader = identityLoader;
        _policyLoader = policyLoader;
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
            count: 1,
            cancellationToken);

        TotalCapabilities = capabilities.Count;
        TotalPolicies = _policyLoader.GetPolicies().Count;
        TotalIdentities = _identityLoader.GetIdentities().Count;
        TotalRelationships = relationships.Count;
        AuditEventsAvailable = auditEvents.Count > 0;
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
}
