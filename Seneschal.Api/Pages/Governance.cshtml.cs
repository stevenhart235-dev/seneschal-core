using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

[IgnoreAntiforgeryToken]
public sealed class GovernanceModel : PageModel
{
    private readonly IGovernanceModeStore _governanceModeStore;
    private readonly IAuditEventStore _auditEventStore;

    public GovernanceModel(
        IGovernanceModeStore governanceModeStore,
        IAuditEventStore auditEventStore)
    {
        _governanceModeStore = governanceModeStore;
        _auditEventStore = auditEventStore;
    }

    public EnforcementMode CurrentMode { get; private set; }

    public string CurrentModeLabel => CurrentMode == EnforcementMode.LogOnly
        ? "LogOnly"
        : "Enforce";

    public string CurrentModeDescription => CurrentMode == EnforcementMode.LogOnly
        ? "Denied and Pending Approval decisions are recorded, but integrated operations continue."
        : "Denied and Pending Approval decisions are projected as blocked.";

    public GovernanceImpactSummary Impact { get; private set; } =
        GovernanceImpactSummary.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentMode = _governanceModeStore.GetMode();
        var events = await _auditEventStore.GetRecentAsync(
            count: 100,
            cancellationToken);
        Impact = CreateImpactSummary(events, DateTimeOffset.UtcNow);
    }

    public IActionResult OnPostSetMode(string mode)
    {
        if (!Enum.TryParse<EnforcementMode>(
                mode,
                ignoreCase: true,
                out var parsedMode))
        {
            return BadRequest();
        }

        _governanceModeStore.SetMode(parsedMode);

        return RedirectToPage();
    }

    public static GovernanceImpactSummary CreateImpactSummary(
        IEnumerable<AuditEvent> auditEvents,
        DateTimeOffset now)
    {
        var events = auditEvents
            .OrderByDescending(auditEvent => auditEvent.TimestampUtc)
            .ToList();
        var activeAfter = now - DashboardModel.ActiveThreshold;
        var activeEvents = events.Where(auditEvent =>
            auditEvent.TimestampUtc >= activeAfter).ToList();

        return new GovernanceImpactSummary(
            activeEvents.Select(auditEvent => auditEvent.IdentityId)
                .Distinct().Count(),
            activeEvents.Select(auditEvent => auditEvent.CapabilityId)
                .Distinct().Count(),
            events.Count(auditEvent => auditEvent.Decision == DecisionType.Allow),
            events.Count(auditEvent => auditEvent.Decision == DecisionType.Deny),
            events.Count(auditEvent =>
                auditEvent.Decision == DecisionType.RequireApproval),
            events.FirstOrDefault(auditEvent =>
                auditEvent.Decision == DecisionType.Deny)?.CapabilityId,
            events.FirstOrDefault(auditEvent =>
                auditEvent.Decision == DecisionType.RequireApproval)?.CapabilityId);
    }
}

public sealed record GovernanceImpactSummary(
    int ActiveIdentities,
    int ActiveCapabilities,
    int RecentAllows,
    int RecentDenials,
    int RecentPendingApprovals,
    string? MostRecentDeniedCapability,
    string? MostRecentPendingCapability)
{
    public static GovernanceImpactSummary Empty { get; } =
        new(0, 0, 0, 0, 0, null, null);
}
