using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class CapabilityActivityModel : PageModel
{
    private readonly IInvestigationActivityReader _investigationActivity;
    private readonly IApprovalStore _approvalStore;
    private readonly ICapabilityCatalog _capabilityCatalog;

    public CapabilityActivityModel(
        IInvestigationActivityReader investigationActivity,
        IApprovalStore approvalStore,
        ICapabilityCatalog capabilityCatalog)
    {
        _investigationActivity = investigationActivity;
        _approvalStore = approvalStore;
        _capabilityCatalog = capabilityCatalog;
    }

    public string? CapabilityId { get; private set; }
    public string? DecisionFilter { get; private set; }
    public string? IdentityFilter { get; private set; }
    public string? EnvironmentFilter { get; private set; }
    public string? OperationIdFilter { get; private set; }
    public string? RuntimeModeFilter { get; private set; }
    public IReadOnlyCollection<CapabilityActivity> Capabilities { get; private set; } = [];
    public CapabilityActivity? SelectedCapability { get; private set; }
    public Capability? SelectedCatalogCapability { get; private set; }
    public IReadOnlyCollection<CapabilityOperationGroup> OperationGroups { get; private set; } = [];
    public IReadOnlyCollection<CapabilityTimelineEvent> LegacyEvents { get; private set; } = [];
    public IReadOnlyCollection<string> AvailableIdentities { get; private set; } = [];
    public IReadOnlyCollection<string> AvailableEnvironments { get; private set; } = [];
    public IReadOnlyCollection<string> AvailableOperations { get; private set; } = [];
    public CapabilityInvestigationSummary Summary { get; private set; } = new();
    public bool CapabilityWasRequested => !string.IsNullOrWhiteSpace(CapabilityId);
    public bool HasActivity => Capabilities.Count > 0;
    public bool HasTimeline => OperationGroups.Count > 0 || LegacyEvents.Count > 0;
    public bool FiltersApplied => !string.IsNullOrWhiteSpace(DecisionFilter) ||
        !string.IsNullOrWhiteSpace(IdentityFilter) ||
        !string.IsNullOrWhiteSpace(EnvironmentFilter) ||
        !string.IsNullOrWhiteSpace(OperationIdFilter) ||
        !string.IsNullOrWhiteSpace(RuntimeModeFilter);

    public async Task OnGetAsync(string? capabilityId, string? decision,
        string? identity, string? environment, string? operationId,
        string? runtimeMode, CancellationToken cancellationToken)
    {
        CapabilityId = capabilityId;
        DecisionFilter = decision;
        IdentityFilter = identity;
        EnvironmentFilter = environment;
        OperationIdFilter = operationId;
        RuntimeModeFilter = runtimeMode;
        var snapshot = await _investigationActivity.GetSnapshotAsync(
            cancellationToken);

        Capabilities = snapshot.Capabilities
            .OrderByDescending(item => item.TotalRequests)
            .ThenByDescending(item => item.DeniedCount)
            .ThenByDescending(item => item.PendingApprovalCount)
            .ThenBy(item => item.CapabilityId).ToList();

        if (string.IsNullOrWhiteSpace(capabilityId)) return;
        SelectedCatalogCapability = (await _capabilityCatalog.GetByIdAsync(
            capabilityId, cancellationToken))?.Capability;
        var investigation = await _investigationActivity.GetCapabilityAsync(
            capabilityId, 100, cancellationToken);
        SelectedCapability = investigation?.Activity;
        if (investigation is null) return;
        var auditEvents = investigation.RecentEvidence.ToList();

        AvailableIdentities = investigation.ObservedIdentities;
        AvailableEnvironments = investigation.Environments;
        AvailableOperations = auditEvents.Select(item => item.ApprovalOperationId)
            .Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();

        Summary = BuildSummary(investigation.Activity, auditEvents);
        var approvals = _approvalStore.GetAll().Where(item => string.Equals(
            item.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase)).ToList();
        var timeline = auditEvents.Select(item => ToTimelineEvent(item,
                approvals.FirstOrDefault(record => string.Equals(record.Id,
                    item.ApprovalId, StringComparison.OrdinalIgnoreCase))))
            .Where(MatchesFilters).ToList();

        OperationGroups = timeline.Where(item => !string.IsNullOrWhiteSpace(item.OperationId))
            .GroupBy(item => item.OperationId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CapabilityOperationGroup(group.Key,
                group.OrderByDescending(item => item.TimestampUtc).ToList(),
                GetOperationState(group)))
            .OrderByDescending(group => group.Events.Max(item => item.TimestampUtc)).ToList();
        LegacyEvents = timeline.Where(item => string.IsNullOrWhiteSpace(item.OperationId))
            .OrderByDescending(item => item.TimestampUtc).ToList();
    }

    public string FilteredAuditLink()
    {
        var parameters = new List<string>();
        Add("capabilityId", CapabilityId);
        Add("identityId", IdentityFilter);
        Add("environment", EnvironmentFilter);
        Add("enforcementMode", RuntimeModeFilter);
        var auditDecision = NormalizeAuditDecision(DecisionFilter);
        Add("decision", auditDecision);
        return parameters.Count == 0 ? "/audit" : $"/audit?{string.Join("&", parameters)}";

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parameters.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    private static string? NormalizeAuditDecision(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "allow" => "allow",
            "deny" => "deny",
            "pendingapproval" or "pending approval" => "requires_approval",
            _ => null
        };

    private bool MatchesFilters(CapabilityTimelineEvent item) =>
        Matches(IdentityFilter, item.IdentityId) &&
        Matches(EnvironmentFilter, item.Environment) &&
        Matches(OperationIdFilter, item.OperationId) &&
        Matches(RuntimeModeFilter, item.RuntimeMode) &&
        (string.IsNullOrWhiteSpace(DecisionFilter) || string.Equals(
            NormalizeDecision(DecisionFilter), item.Decision,
            StringComparison.OrdinalIgnoreCase) || string.Equals(
            DecisionFilter, item.ApprovalStatus, StringComparison.OrdinalIgnoreCase));

    private static bool Matches(string? filter, string? value) =>
        string.IsNullOrWhiteSpace(filter) || string.Equals(filter, value,
            StringComparison.OrdinalIgnoreCase);

    private static CapabilityInvestigationSummary BuildSummary(
        CapabilityActivity activity,
        IReadOnlyCollection<AuditEvent> events)
    {
        var evaluations = events.Where(item => item.ApprovalAction is not
            ("Approved" or "Rejected")).ToList();
        var correlated = evaluations.Where(item =>
            !string.IsNullOrWhiteSpace(item.ApprovalOperationId)).ToList();
        var operationStates = events.Where(item =>
                !string.IsNullOrWhiteSpace(item.ApprovalOperationId))
            .GroupBy(item => item.ApprovalOperationId!,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => GetOperationState(group.Select(item =>
                ToTimelineEvent(item, null))))
            .ToList();
        return new CapabilityInvestigationSummary
    {
        TotalEvaluations = activity.TotalRequests,
        AllowCount = activity.AllowedCount,
        DenyCount = activity.DeniedCount,
        PendingApprovalCount = activity.PendingApprovalCount,
        RecentEvaluationCount = evaluations.Count,
        RecentAllowCount = evaluations.Count(item => item.Decision == DecisionType.Allow),
        RecentDenyCount = evaluations.Count(item => item.Decision == DecisionType.Deny),
        DistinctOperations = events.Select(item => item.ApprovalOperationId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
        CorrelatedEvaluationCount = correlated.Count,
        LegacyEvaluationCount = evaluations.Count - correlated.Count,
        ApprovedOperationCount = operationStates.Count(item => item == "Approved"),
        RejectedOperationCount = operationStates.Count(item => item == "Rejected"),
        ConsumedOperationCount = operationStates.Count(item => item == "Consumed"),
        PendingOperationCount = operationStates.Count(item => item == "Pending"),
        MostRecentActivityUtc = activity.LastUsedUtc
    };
    }

    public string GetInvestigationSummaryText()
    {
        var summary = Summary;
        if (summary.TotalEvaluations == 0)
            return "No evaluation activity has been recorded for this capability.";

        if (summary.DistinctOperations == 0)
            return $"Recent activity includes {Count(summary.LegacyEvaluationCount, "legacy evaluation", "legacy evaluations")} without Operation IDs; distinct operations cannot be determined.";

        string correlated;
        if (summary.RejectedOperationCount == summary.DistinctOperations)
        {
            correlated = $"{Count(summary.DistinctOperations, "operation was", "operations were")} rejected after {Count(summary.CorrelatedEvaluationCount, "evaluation attempt", "evaluation attempts")}.";
        }
        else if (summary.ConsumedOperationCount == summary.DistinctOperations)
        {
            correlated = $"{Count(summary.DistinctOperations, "operation reached", "operations reached")} Allow and consumed {Possessive(summary.DistinctOperations)} approval after {Count(summary.CorrelatedEvaluationCount, "evaluation attempt", "evaluation attempts")}.";
        }
        else if (summary.ApprovedOperationCount == summary.DistinctOperations)
        {
            var subject = summary.DistinctOperations == 1
                ? "1 operation has been approved and is"
                : $"{summary.DistinctOperations} operations have been approved and are";
            correlated = $"{subject} awaiting a retry after {Count(summary.CorrelatedEvaluationCount, "evaluation attempt", "evaluation attempts")}.";
        }
        else if (summary.PendingOperationCount == summary.DistinctOperations &&
                 summary.RecentAllowCount == 0 && summary.RecentDenyCount == 0)
        {
            correlated = $"{Count(summary.DistinctOperations, "operation is", "operations are")} awaiting approval after {Count(summary.CorrelatedEvaluationCount, "evaluation attempt", "evaluation attempts")}.";
        }
        else if (summary.RecentAllowCount == summary.CorrelatedEvaluationCount)
        {
            correlated = $"Recent activity includes {Count(summary.CorrelatedEvaluationCount, "Allow evaluation attempt", "Allow evaluation attempts")} across {Count(summary.DistinctOperations, "operation", "operations")}.";
        }
        else if (summary.RecentDenyCount == summary.CorrelatedEvaluationCount)
        {
            correlated = $"Recent activity includes {Count(summary.CorrelatedEvaluationCount, "denied evaluation attempt", "denied evaluation attempts")} across {Count(summary.DistinctOperations, "operation", "operations")}.";
        }
        else
        {
            correlated = $"Recent activity includes {Count(summary.CorrelatedEvaluationCount, "evaluation attempt", "evaluation attempts")} across {Count(summary.DistinctOperations, "operation", "operations")}: {Count(summary.RecentAllowCount, "Allow", "Allows")}, {Count(summary.RecentDenyCount, "Deny", "Denies")}, and {Count(summary.RecentEvaluationCount - summary.RecentAllowCount - summary.RecentDenyCount, "Pending Approval", "Pending Approvals")}.";
        }

        return summary.LegacyEvaluationCount == 0
            ? correlated
            : $"{correlated} {Count(summary.LegacyEvaluationCount, "legacy evaluation lacks", "legacy evaluations lack")} an Operation ID and cannot be assigned to a distinct operation.";
    }

    private static string Count(long count, string singular, string plural) =>
        $"{count} {(count == 1 ? singular : plural)}";
    private static string Possessive(int count) => count == 1 ? "its" : "their";

    private static CapabilityTimelineEvent ToTimelineEvent(AuditEvent item,
        ApprovalRecord? approval)
    {
        var decision = DecisionLabel(item.Decision);
        var status = item.ApprovalStatus ?? approval?.Status.ToString();
        var lifecycle = item.ApprovalAction is "Approved" or "Rejected"
            ? item.ApprovalAction
            : item.ApprovalStatus == "Consumed" ? "Consumed" : null;
        return new CapabilityTimelineEvent(item.Id, item.TimestampUtc,
            item.IdentityId, item.CapabilityId, item.Environment, item.ResourceId,
            decision, item.EnforcementMode.ToString(), item.ApprovalOperationId,
            item.ExecutionGuidance, item.MatchedPolicies.FirstOrDefault(), item.Reason,
            status, lifecycle, EffectiveAction(item));
    }

    private static string EffectiveAction(AuditEvent item) =>
        item.ApprovalAction is "Approved" or "Rejected"
            ? "Approval resolved"
            :
        (item.Decision, item.EnforcementMode) switch
        {
            (DecisionType.Allow, _) => "Caller may proceed",
            (DecisionType.Deny, EnforcementMode.LogOnly) => "Recorded; caller may continue",
            (DecisionType.Deny, _) => "Caller should block the operation",
            (DecisionType.RequireApproval, EnforcementMode.LogOnly) => "Recorded; caller may continue",
            (DecisionType.RequireApproval, _) => "Caller should pause and retry",
            _ => "Recorded"
        };

    private static string GetOperationState(IEnumerable<CapabilityTimelineEvent> events)
    {
        var ordered = events.OrderByDescending(item => item.TimestampUtc).ToList();
        if (ordered.Any(item => item.ApprovalStatus == "Consumed")) return "Consumed";
        if (ordered.Any(item => item.ApprovalStatus == "Rejected")) return "Rejected";
        if (ordered.Any(item => item.ApprovalStatus == "Approved")) return "Approved";
        if (ordered.Any(item => item.Decision == "Pending Approval")) return "Pending";
        return ordered.FirstOrDefault()?.Decision ?? "Recorded";
    }

    private static string DecisionLabel(DecisionType decision) =>
        decision == DecisionType.RequireApproval ? "Pending Approval" : decision.ToString();
    private static string NormalizeDecision(string value) =>
        string.Equals(value, "PendingApproval", StringComparison.OrdinalIgnoreCase)
            ? "Pending Approval" : value;
}

public sealed record CapabilityTimelineEvent(string DecisionId,
    DateTimeOffset TimestampUtc, string IdentityId, string CapabilityId,
    string Environment, string ResourceId, string Decision, string RuntimeMode,
    string? OperationId, string ExecutionGuidance, string? MatchedPolicy,
    string Reason, string? ApprovalStatus, string? LifecycleEvent,
    string EffectiveAction);

public sealed record CapabilityOperationGroup(string OperationId,
    IReadOnlyCollection<CapabilityTimelineEvent> Events, string State);

public sealed record CapabilityInvestigationSummary
{
    public long TotalEvaluations { get; init; }
    public long AllowCount { get; init; }
    public long DenyCount { get; init; }
    public long PendingApprovalCount { get; init; }
    public int RecentEvaluationCount { get; init; }
    public int RecentAllowCount { get; init; }
    public int RecentDenyCount { get; init; }
    public int DistinctOperations { get; init; }
    public int CorrelatedEvaluationCount { get; init; }
    public int LegacyEvaluationCount { get; init; }
    public int ApprovedOperationCount { get; init; }
    public int RejectedOperationCount { get; init; }
    public int ConsumedOperationCount { get; init; }
    public int PendingOperationCount { get; init; }
    public DateTimeOffset? MostRecentActivityUtc { get; init; }
}
