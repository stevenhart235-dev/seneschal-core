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
    public long CurrentModeVersion { get; private set; }

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
        var state = _governanceModeStore.GetState();
        CurrentMode = state.Mode;
        CurrentModeVersion = state.Version;
        var events = await _auditEventStore.GetRecentAsync(
            count: 100,
            cancellationToken);
        Impact = CreateImpactSummary(events, DateTimeOffset.UtcNow);
    }

    public async Task<IActionResult> OnPostSetModeAsync(
        string mode, long? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EnforcementMode>(
                mode,
                ignoreCase: true,
                out var parsedMode))
        {
            return BadRequest();
        }

        try
        {
            await _governanceModeStore.SetModeAsync(parsedMode,
                expectedVersion ?? _governanceModeStore.GetState().Version,
                reason: "Runtime governance mode changed through the operator portal.",
                cancellationToken: cancellationToken);
        }
        catch (Seneschal.Core.Exceptions.OperationalControlConcurrencyException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return StatusCode(StatusCodes.Status409Conflict, ModelState);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "The runtime governance change could not be persisted. Retry the request.");
        }

        return RedirectToPage();
    }

    public static GovernanceImpactSummary CreateImpactSummary(
        IEnumerable<AuditEvent> auditEvents,
        DateTimeOffset now)
    {
        var events = auditEvents
            .Where(auditEvent => !IsAdministrativeEvidence(
                auditEvent.EffectiveAction))
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

    private static bool IsAdministrativeEvidence(string effectiveAction) =>
        effectiveAction.StartsWith("approval_", StringComparison.Ordinal) ||
        effectiveAction.StartsWith("runtime_mode_", StringComparison.Ordinal) ||
        effectiveAction.StartsWith("governance_window_", StringComparison.Ordinal) ||
        effectiveAction.StartsWith("incident_", StringComparison.Ordinal);
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
