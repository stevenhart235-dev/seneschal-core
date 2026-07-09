using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class IncidentDetailModel : PageModel
{
    private readonly IGovernanceIncidentStore _incidentStore;
    private readonly IAuditEventStore _auditEventStore;

    public IncidentDetailModel(
        IGovernanceIncidentStore incidentStore,
        IAuditEventStore auditEventStore)
    {
        _incidentStore = incidentStore;
        _auditEventStore = auditEventStore;
    }

    public GovernanceIncident? Incident { get; private set; }

    public IReadOnlyCollection<AuditEvent> RecentAuditEvents { get; private set; } =
        [];

    public string IncidentId { get; private set; } = string.Empty;

    public bool IncidentNotFound => Incident is null;

    public async Task<IActionResult> OnGetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        IncidentId = id;
        Incident = await _incidentStore.GetByIdAsync(id, cancellationToken);

        if (Incident is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;

            if (!AcceptsHtml())
            {
                return NotFound(new
                {
                    status = StatusCodes.Status404NotFound,
                    title = "Incident not found",
                    incidentId = id
                });
            }

            return Page();
        }

        RecentAuditEvents = (await _auditEventStore.GetRecentAsync(
            count: 100,
            cancellationToken))
            .Where(MatchesIncident)
            .Take(5)
            .ToList();

        if (!AcceptsHtml())
        {
            return new JsonResult(new
            {
                Incident.Id,
                Incident.Title,
                Severity = Incident.Severity.ToString(),
                Incident.CapabilityId,
                Incident.IdentityId,
                Incident.DecisionReason,
                Incident.MatchedPolicy,
                Incident.FirstSeenUtc,
                Incident.LastSeenUtc,
                Incident.OccurrenceCount,
                CurrentStatus = Incident.CurrentStatus.ToString()
            });
        }

        return Page();
    }

    public string AuditLink()
    {
        return Incident is null
            ? "/audit"
            : IncidentsModel.AuditLink(Incident);
    }

    public string CapabilityLink()
    {
        return Incident is null
            ? "/capability-explorer"
            : $"/capability-explorer?capabilityId={Uri.EscapeDataString(Incident.CapabilityId)}";
    }

    public string IdentityLink()
    {
        return Incident is null
            ? "/identity-activity"
            : $"/identity-activity?identityId={Uri.EscapeDataString(Incident.IdentityId)}";
    }

    public string PolicyLink()
    {
        return "/policies";
    }

    public string GetRecommendation()
    {
        if (Incident is null)
        {
            return string.Empty;
        }

        return Incident.Severity switch
        {
            GovernanceIncidentSeverity.Critical =>
                "Repeated denied access detected. Review policy and caller.",
            GovernanceIncidentSeverity.Warning =>
                "Repeated approval requests detected. Consider refining policy.",
            _ =>
                "Monitor future activity before taking action."
        };
    }

    public string GetAge()
    {
        if (Incident is null)
        {
            return "n/a";
        }

        var age = DateTimeOffset.UtcNow - Incident.FirstSeenUtc;

        if (age.TotalDays >= 1)
        {
            return $"{Math.Floor(age.TotalDays)}d";
        }

        if (age.TotalHours >= 1)
        {
            return $"{Math.Floor(age.TotalHours)}h";
        }

        if (age.TotalMinutes >= 1)
        {
            return $"{Math.Floor(age.TotalMinutes)}m";
        }

        return "just now";
    }

    public static string SeverityClass(GovernanceIncidentSeverity severity)
    {
        return IncidentsModel.SeverityClass(severity);
    }

    public static string DecisionClass(DecisionType decision)
    {
        return decision switch
        {
            DecisionType.Allow => "decision-allow",
            DecisionType.Deny => "decision-deny",
            DecisionType.RequireApproval => "decision-pending",
            DecisionType.Warn => "decision-warn",
            _ => "decision-log-only"
        };
    }

    private bool MatchesIncident(AuditEvent auditEvent)
    {
        if (Incident is null)
        {
            return false;
        }

        var policyMatches = string.IsNullOrWhiteSpace(Incident.MatchedPolicy) ||
            auditEvent.MatchedPolicies.Any(policy => string.Equals(
                policy,
                Incident.MatchedPolicy,
                StringComparison.OrdinalIgnoreCase));

        return string.Equals(
                auditEvent.CapabilityId,
                Incident.CapabilityId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                auditEvent.IdentityId,
                Incident.IdentityId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                auditEvent.Reason,
                Incident.DecisionReason,
                StringComparison.OrdinalIgnoreCase) &&
            policyMatches;
    }

    private bool AcceptsHtml()
    {
        return Request.Headers.Accept.ToString().Contains(
            "text/html",
            StringComparison.OrdinalIgnoreCase);
    }
}
