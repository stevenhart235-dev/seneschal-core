using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class MonitorModel : PageModel
{
    private readonly PolicyLoader _policyLoader;
    private readonly IGovernanceModeStore _governanceModeStore;
    private readonly IAuditEventStore _auditEventStore;
    private readonly IActivityStore _activityStore;
    private readonly CapabilityLoader _capabilityLoader;
    private readonly IdentityLoader _identityLoader;
    private readonly IGovernanceWindowStore _governanceWindowStore;
    private readonly IApprovalStore _approvalStore;
    private readonly IGovernanceIncidentStore _incidentStore;

    public static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ActiveThreshold = TimeSpan.FromSeconds(20);

    public MonitorModel(
        PolicyLoader policyLoader,
        IGovernanceModeStore governanceModeStore,
        IAuditEventStore auditEventStore,
        IActivityStore activityStore,
        CapabilityLoader capabilityLoader,
        IdentityLoader identityLoader,
        IGovernanceWindowStore governanceWindowStore,
        IApprovalStore approvalStore,
        IGovernanceIncidentStore incidentStore)
    {
        _policyLoader = policyLoader;
        _governanceModeStore = governanceModeStore;
        _auditEventStore = auditEventStore;
        _activityStore = activityStore;
        _capabilityLoader = capabilityLoader;
        _identityLoader = identityLoader;
        _governanceWindowStore = governanceWindowStore;
        _approvalStore = approvalStore;
        _incidentStore = incidentStore;
    }

    public string CurrentMode => _governanceModeStore.GetMode() == EnforcementMode.LogOnly
        ? "Monitor"
        : "Enforce";

    public string CanonicalRuntimeMode => _governanceModeStore.GetMode().ToString();
    public GovernanceWindow GovernanceWindow { get; private set; } = null!;
    public IReadOnlyCollection<AuditEvent> RecentEvaluations { get; private set; } = [];
    public IReadOnlyCollection<GovernanceIncident> RecentIncidents { get; private set; } = [];
    public DateTimeOffset GeneratedAtUtc { get; private set; }
    public DateTimeOffset? LastEvaluationUtc { get; private set; }
    public long RecentEvaluationCount { get; private set; }
    public long RecentAllowCount { get; private set; }
    public long RecentDenyCount { get; private set; }
    public long RecentPendingCount { get; private set; }
    public double RecentAverageLatencyMs { get; private set; }
    public long RecentMinimumLatencyMs { get; private set; }
    public long RecentMaximumLatencyMs { get; private set; }
    public int ActiveIdentityCount { get; private set; }
    public int ActiveCapabilityCount { get; private set; }
    public int CurrentPendingApprovalCount { get; private set; }
    public CapabilityActivity? MostDeniedCapability { get; private set; }
    public IdentityActivity? MostActiveIdentity { get; private set; }
    public bool EvaluationsFlowing => LastEvaluationUtc is not null &&
        LastEvaluationUtc >= GeneratedAtUtc - ActiveThreshold;
    public bool SeneschalHealthy => _capabilityLoader.GetCapabilities().Count > 0 &&
        _identityLoader.GetIdentities().Count > 0 && _policyLoader.GetPolicies().Count > 0;
    public string OperationalSummary => CanonicalRuntimeMode == "Enforce"
        ? GovernanceWindow.Enabled
            ? $"Runtime enforcement is active with {GovernanceWindow.Name}."
            : "Runtime enforcement is active; denied and pending decisions may block callers."
        : GovernanceWindow.Enabled
            ? $"Monitoring is active with {GovernanceWindow.Name} participating in evaluations."
            : "Monitoring is active; denied and pending decisions are recorded without blocking.";

    public ActivitySnapshot Activity { get; private set; } = new();
    public IReadOnlyCollection<AuditEvent> AuditEvents { get; private set; } = [];
    public int ReadinessScore { get; private set; }
    public string ReadinessRecommendation { get; private set; } = string.Empty;
    public bool PoliciesExist { get; private set; }
    public bool RuntimeDecisionsObserved { get; private set; }
    public bool AuditHistoryAvailable { get; private set; }
    public bool ActivityMetricsAvailable { get; private set; }
    public long TotalDeniedDecisions { get; private set; }
    public long PendingApprovalDecisions { get; private set; }
    public IReadOnlyCollection<DenialReasonSummary> TopDenialReasons
        { get; private set; } = [];
    public IReadOnlyCollection<PolicyActivity> MostActivePolicies
        { get; private set; } = [];
    public IReadOnlyCollection<CapabilityReadiness> CapabilityReadiness
        { get; private set; } = [];
    public IReadOnlyCollection<EnforcementReadinessAdvice>
        EnforcementReadinessAdvisor { get; private set; } = [];
    public IReadOnlyCollection<UnusedCapabilityDrift> UnusedCapabilities
        { get; private set; } = [];
    public IReadOnlyCollection<UnusedIdentityDrift> UnusedIdentities
        { get; private set; } = [];
    public IReadOnlyCollection<UnusedPolicyDrift> UnusedPolicies
        { get; private set; } = [];
    public IReadOnlyCollection<ActivityWithoutPolicyMatchDrift>
        ActivityWithoutPolicyMatches { get; private set; } = [];
    public IReadOnlyCollection<GovernanceRecommendation>
        GovernanceRecommendations { get; private set; } = [];

    public bool HasGovernanceDrift =>
        UnusedCapabilities.Count > 0 ||
        UnusedIdentities.Count > 0 ||
        UnusedPolicies.Count > 0 ||
        ActivityWithoutPolicyMatches.Count > 0;

    public bool HasWouldHaveBeenBlockedActivity =>
        TotalDeniedDecisions > 0 || PendingApprovalDecisions > 0;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow;
        GovernanceWindow = _governanceWindowStore.GetWindow();
        Activity = await _activityStore.GetSnapshotAsync(cancellationToken);
        AuditEvents = await _auditEventStore.GetRecentAsync(
            cancellationToken: cancellationToken);
        var recent = AuditEvents
            .Where(item => item.TimestampUtc >= GeneratedAtUtc - RecentWindow)
            .OrderByDescending(item => item.TimestampUtc)
            .ToList();
        RecentEvaluations = recent.Take(20).ToList();
        LastEvaluationUtc = AuditEvents
            .OrderByDescending(item => item.TimestampUtc)
            .FirstOrDefault()?.TimestampUtc;
        RecentEvaluationCount = recent.Count;
        RecentAllowCount = recent.LongCount(item => item.Decision == DecisionType.Allow);
        RecentDenyCount = recent.LongCount(item => item.Decision == DecisionType.Deny);
        RecentPendingCount = recent.LongCount(item => item.Decision == DecisionType.RequireApproval);
        RecentAverageLatencyMs = recent.Count == 0
            ? 0 : recent.Average(item => item.EvaluationDurationMs);
        RecentMinimumLatencyMs = recent.Count == 0
            ? 0 : recent.Min(item => item.EvaluationDurationMs);
        RecentMaximumLatencyMs = recent.Count == 0
            ? 0 : recent.Max(item => item.EvaluationDurationMs);
        var activeAfter = GeneratedAtUtc - ActiveThreshold;
        ActiveIdentityCount = recent
            .Where(item => item.TimestampUtc >= activeAfter)
            .Select(item => item.IdentityId)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        ActiveCapabilityCount = recent
            .Where(item => item.TimestampUtc >= activeAfter)
            .Select(item => item.CapabilityId)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        CurrentPendingApprovalCount = _approvalStore.GetAll()
            .Count(item => item.Status == ApprovalStatus.Pending);
        MostDeniedCapability = Activity.Capabilities
            .Where(item => item.DeniedCount > 0)
            .OrderByDescending(item => item.DeniedCount)
            .ThenBy(item => item.CapabilityId)
            .FirstOrDefault();
        MostActiveIdentity = Activity.Identities
            .OrderByDescending(item => item.TotalRequests)
            .ThenBy(item => item.IdentityId)
            .FirstOrDefault();
        RecentIncidents = (await _incidentStore.GetAllAsync(cancellationToken))
            .OrderByDescending(item => item.LastSeenUtc)
            .Take(3).ToList();

        PoliciesExist = _policyLoader.GetPolicies().Count > 0;
        RuntimeDecisionsObserved = Activity.Capabilities.Any(capability =>
            capability.TotalRequests > 0);
        AuditHistoryAvailable = AuditEvents.Count > 0;
        ActivityMetricsAvailable =
            Activity.Capabilities.Count > 0 ||
            Activity.Identities.Count > 0 ||
            Activity.Policies.Count > 0;

        ReadinessScore = new[]
        {
            PoliciesExist,
            RuntimeDecisionsObserved,
            AuditHistoryAvailable,
            ActivityMetricsAvailable
        }.Count(criterion => criterion) * 25;
        ReadinessRecommendation = BuildRecommendation(ReadinessScore);

        TotalDeniedDecisions = Activity.Capabilities.Sum(capability =>
            capability.DeniedCount);
        PendingApprovalDecisions = Activity.Capabilities.Sum(capability =>
            capability.PendingApprovalCount);

        TopDenialReasons = AuditEvents
            .Where(auditEvent => auditEvent.Decision == DecisionType.Deny)
            .GroupBy(auditEvent => auditEvent.Reason)
            .Select(group => new DenialReasonSummary(
                string.IsNullOrWhiteSpace(group.Key)
                    ? "No reason provided"
                    : group.Key,
                group.LongCount()))
            .OrderByDescending(summary => summary.Count)
            .ThenBy(summary => summary.Reason)
            .Take(5)
            .ToList();

        MostActivePolicies = Activity.Policies
            .OrderByDescending(policy => policy.MatchCount)
            .ThenBy(policy => policy.PolicyId)
            .Take(5)
            .ToList();

        CapabilityReadiness = Activity.Capabilities
            .OrderByDescending(capability => capability.TotalRequests)
            .ThenBy(capability => capability.CapabilityId)
            .Take(10)
            .Select(capability => new CapabilityReadiness(
                capability.CapabilityId,
                capability.TotalRequests,
                capability.DeniedCount,
                capability.PendingApprovalCount,
                capability.TotalRequests > 0 &&
                    capability.DeniedCount == 0 &&
                    capability.PendingApprovalCount == 0
                    ? "Ready"
                    : "Needs More Observation"))
            .ToList();

        EnforcementReadinessAdvisor = Activity.Capabilities
            .Where(capability => capability.TotalRequests > 0)
            .Select(BuildEnforcementReadinessAdvice)
            .OrderByDescending(advice => advice.ReadinessScore)
            .ThenByDescending(advice => advice.TotalRequests)
            .ThenBy(advice => advice.CapabilityId)
            .ToList();

        BuildGovernanceDrift();
        GovernanceRecommendations = BuildGovernanceRecommendations();
    }

    private static string BuildRecommendation(int score)
    {
        return score switch
        {
            >= 100 => "Ready for targeted enforcement on stable capability paths.",
            >= 75 => "Review denied and pending decisions before enabling enforcement.",
            >= 50 => "Continue monitoring until activity and audit history are broader.",
            _ => "Start with inventory and monitor-mode runtime evaluations."
        };
    }

    private static EnforcementReadinessAdvice BuildEnforcementReadinessAdvice(
        CapabilityActivity capability)
    {
        var score = 100;
        var reasons = new List<string>();

        if (capability.DeniedCount > 0)
        {
            score -= 25;
            reasons.Add("Runtime denials still occurring");
        }
        else
        {
            reasons.Add("No denied evaluations observed");
        }

        if (capability.PendingApprovalCount > 0)
        {
            score -= 20;
            reasons.Add("Pending approvals still being generated");
        }
        else
        {
            reasons.Add("No pending approvals observed");
        }

        if (capability.TotalRequests < 25)
        {
            score -= 15;
            reasons.Add("Limited runtime history");
        }
        else
        {
            reasons.Add("Stable runtime behavior observed");
        }

        if (capability.AverageEvaluationDurationMs > 10)
        {
            score -= 10;
            reasons.Add("Evaluation latency may need review");
        }

        if (capability.AllowedCount == 0)
        {
            score -= 20;
            reasons.Add("No successful allowed evaluations observed");
        }

        score = Math.Clamp(score, 0, 100);
        var status = BuildEnforcementReadinessStatus(score);

        return new EnforcementReadinessAdvice(
            capability.CapabilityId,
            score,
            status,
            capability.TotalRequests,
            capability.AllowedCount,
            capability.DeniedCount,
            capability.PendingApprovalCount,
            capability.AverageEvaluationDurationMs,
            BuildEnforcementRecommendation(status),
            reasons);
    }

    private static string BuildEnforcementReadinessStatus(int score)
    {
        return score switch
        {
            >= 90 => "Ready for Enforcement",
            >= 70 => "Nearly Ready",
            >= 40 => "Remain in Monitor",
            _ => "Not Ready"
        };
    }

    private static string BuildEnforcementRecommendation(string status)
    {
        return status switch
        {
            "Ready for Enforcement" =>
                "Candidate for targeted enforcement after owner review.",
            "Nearly Ready" =>
                "Review remaining signals before enabling enforcement.",
            "Remain in Monitor" =>
                "Keep observing until denials, approvals, and volume stabilize.",
            _ =>
                "Do not enforce yet; gather successful activity and resolve blockers."
        };
    }

    private void BuildGovernanceDrift()
    {
        var activeCapabilities = Activity.Capabilities
            .Where(capability => capability.TotalRequests > 0)
            .Select(capability => capability.CapabilityId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeIdentities = Activity.Identities
            .Where(identity => identity.TotalRequests > 0)
            .Select(identity => identity.IdentityId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedPolicies = Activity.Policies
            .Where(policy => policy.MatchCount > 0)
            .Select(policy => policy.PolicyId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        UnusedCapabilities = _capabilityLoader.GetCapabilities()
            .Where(capability => !activeCapabilities.Contains(capability.Name))
            .OrderBy(capability => capability.Name)
            .Select(capability => new UnusedCapabilityDrift(
                capability.Name,
                capability.Name,
                capability.Risk,
                capability.Category))
            .ToList();

        UnusedIdentities = _identityLoader.GetIdentities()
            .Where(identity => !activeIdentities.Contains(identity.Name))
            .OrderBy(identity => identity.Name)
            .Select(identity => new UnusedIdentityDrift(
                identity.Name,
                string.IsNullOrWhiteSpace(identity.Description)
                    ? identity.Name
                    : identity.Description,
                identity.Type,
                "YAML"))
            .ToList();

        UnusedPolicies = _policyLoader.GetPolicies()
            .Where(policy => !matchedPolicies.Contains(policy.Name))
            .OrderBy(policy => policy.Name)
            .Select(policy => new UnusedPolicyDrift(
                policy.Name,
                policy.Name,
                policy.Decision,
                CurrentMode))
            .ToList();

        ActivityWithoutPolicyMatches = AuditEvents
            .Where(auditEvent => activeCapabilities.Contains(
                auditEvent.CapabilityId))
            .Where(auditEvent => auditEvent.MatchedPolicies.Count == 0)
            .GroupBy(auditEvent => auditEvent.CapabilityId)
            .Select(group => new ActivityWithoutPolicyMatchDrift(
                group.Key,
                group.LongCount()))
            .OrderByDescending(drift => drift.EventCount)
            .ThenBy(drift => drift.CapabilityId)
            .ToList();
    }

    private IReadOnlyCollection<GovernanceRecommendation>
        BuildGovernanceRecommendations()
    {
        var recommendations = new List<GovernanceRecommendation>();

        foreach (var advice in EnforcementReadinessAdvisor)
        {
            if (advice.ReadinessScore >= 90)
            {
                recommendations.Add(new GovernanceRecommendation(
                    "Ready to Enforce",
                    "Info",
                    "Consider moving this capability to Enforce mode.",
                    [
                        $"Capability: {advice.CapabilityId}",
                        $"Readiness score: {advice.ReadinessScore}%",
                        .. advice.ExplanationReasons
                    ],
                    $"/capability-activity?capabilityId={Uri.EscapeDataString(advice.CapabilityId)}",
                    "View capability activity"));
            }
            else
            {
                recommendations.Add(new GovernanceRecommendation(
                    "Continue Monitoring",
                    "Warning",
                    "Continue monitoring before enforcing.",
                    [
                        $"Capability: {advice.CapabilityId}",
                        $"Readiness score: {advice.ReadinessScore}%",
                        .. advice.ExplanationReasons
                    ],
                    $"/capability-activity?capabilityId={Uri.EscapeDataString(advice.CapabilityId)}",
                    "View capability activity"));
            }
        }

        if (UnusedCapabilities.Count > 0 ||
            UnusedIdentities.Count > 0 ||
            UnusedPolicies.Count > 0)
        {
            recommendations.Add(new GovernanceRecommendation(
                "Review Unused Governance Objects",
                "Warning",
                "Review or archive unused governance objects.",
                BuildUnusedGovernanceEvidence(),
                "/monitor",
                "Review governance drift"));
        }

        foreach (var gap in BuildPolicyCoverageGapRecommendations())
        {
            recommendations.Add(gap);
        }

        foreach (var capability in Activity.Capabilities
            .Where(capability => capability.DeniedCount > 0)
            .OrderByDescending(capability => capability.DeniedCount)
            .ThenBy(capability => capability.CapabilityId))
        {
            recommendations.Add(new GovernanceRecommendation(
                "Review High-Denial Capability",
                "Critical",
                "Review denial patterns before enforcement.",
                [
                    $"Capability: {capability.CapabilityId}",
                    $"Denied evaluations: {capability.DeniedCount}"
                ],
                $"/audit?capabilityId={Uri.EscapeDataString(capability.CapabilityId)}",
                "View related audit events"));
        }

        return recommendations;
    }

    private IReadOnlyCollection<string> BuildUnusedGovernanceEvidence()
    {
        var evidence = new List<string>
        {
            $"Unused capabilities: {UnusedCapabilities.Count}",
            $"Unused identities: {UnusedIdentities.Count}",
            $"Unused policies: {UnusedPolicies.Count}"
        };

        evidence.AddRange(UnusedCapabilities
            .Take(3)
            .Select(capability => $"Capability example: {capability.CapabilityId}"));
        evidence.AddRange(UnusedIdentities
            .Take(3)
            .Select(identity => $"Identity example: {identity.IdentityId}"));
        evidence.AddRange(UnusedPolicies
            .Take(3)
            .Select(policy => $"Policy example: {policy.PolicyId}"));

        return evidence;
    }

    private IReadOnlyCollection<GovernanceRecommendation>
        BuildPolicyCoverageGapRecommendations()
    {
        return AuditEvents
            .Where(auditEvent => auditEvent.MatchedPolicies.Count == 0)
            .GroupBy(auditEvent => new
            {
                auditEvent.CapabilityId,
                auditEvent.IdentityId
            })
            .OrderBy(group => group.Key.CapabilityId)
            .ThenBy(group => group.Key.IdentityId)
            .Select(group => new GovernanceRecommendation(
                "Policy Coverage Gap",
                "Critical",
                "Create or update policy coverage for observed runtime activity.",
                [
                    $"Capability: {group.Key.CapabilityId}",
                    $"Identity: {group.Key.IdentityId}",
                    $"Observed evaluations without matched policy: {group.LongCount()}"
                ],
                $"/audit?capabilityId={Uri.EscapeDataString(group.Key.CapabilityId)}",
                "View related audit events"))
            .ToList();
    }
}

public sealed record DenialReasonSummary(string Reason, long Count);

public sealed record CapabilityReadiness(
    string CapabilityId,
    long TotalRequests,
    long DeniedCount,
    long PendingApprovalCount,
    string Status);

public sealed record EnforcementReadinessAdvice(
    string CapabilityId,
    int ReadinessScore,
    string Status,
    long TotalRequests,
    long AllowedCount,
    long DeniedCount,
    long PendingApprovalCount,
    double AverageEvaluationDurationMs,
    string Recommendation,
    IReadOnlyCollection<string> ExplanationReasons);

public sealed record UnusedCapabilityDrift(
    string CapabilityId,
    string Name,
    string Risk,
    string Category);

public sealed record UnusedIdentityDrift(
    string IdentityId,
    string Name,
    string Type,
    string Source);

public sealed record UnusedPolicyDrift(
    string PolicyId,
    string Name,
    string Effect,
    string Mode);

public sealed record ActivityWithoutPolicyMatchDrift(
    string CapabilityId,
    long EventCount);

public sealed record GovernanceRecommendation(
    string Title,
    string Severity,
    string RecommendationText,
    IReadOnlyCollection<string> Evidence,
    string? LinkUrl = null,
    string? LinkText = null);
