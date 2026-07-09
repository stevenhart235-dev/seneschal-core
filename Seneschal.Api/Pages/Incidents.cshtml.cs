using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class IncidentsModel : PageModel
{
    private readonly IGovernanceIncidentStore _incidentStore;

    public IncidentsModel(IGovernanceIncidentStore incidentStore)
    {
        _incidentStore = incidentStore;
    }

    public IReadOnlyCollection<GovernanceIncident> Incidents { get; private set; } =
        [];

    public int OpenIncidentCount { get; private set; }

    public int CriticalIncidentCount { get; private set; }

    public int WarningIncidentCount { get; private set; }

    public int TotalOccurrenceCount { get; private set; }

    public bool HasIncidents => Incidents.Count > 0;

    public async Task<IActionResult> OnGetAsync(
        CancellationToken cancellationToken)
    {
        Incidents = await _incidentStore.GetAllAsync(cancellationToken);

        OpenIncidentCount = Incidents.Count(incident =>
            incident.CurrentStatus == GovernanceIncidentStatus.Open);
        CriticalIncidentCount = Incidents.Count(incident =>
            incident.Severity == GovernanceIncidentSeverity.Critical);
        WarningIncidentCount = Incidents.Count(incident =>
            incident.Severity == GovernanceIncidentSeverity.Warning);
        TotalOccurrenceCount = Incidents.Sum(incident =>
            incident.OccurrenceCount);

        if (!AcceptsHtml())
        {
            return new JsonResult(Incidents.Select(incident => new
            {
                incident.Id,
                incident.Title,
                Severity = incident.Severity.ToString(),
                incident.CapabilityId,
                incident.IdentityId,
                incident.DecisionReason,
                incident.MatchedPolicy,
                incident.FirstSeenUtc,
                incident.LastSeenUtc,
                incident.OccurrenceCount,
                CurrentStatus = incident.CurrentStatus.ToString()
            }));
        }

        return Page();
    }

    public static string SeverityClass(GovernanceIncidentSeverity severity)
    {
        return severity switch
        {
            GovernanceIncidentSeverity.Critical => "severity-critical",
            GovernanceIncidentSeverity.Warning => "severity-warning",
            _ => "severity-info"
        };
    }

    public static string AuditLink(GovernanceIncident incident)
    {
        return
            $"/audit?capabilityId={Uri.EscapeDataString(incident.CapabilityId)}" +
            $"&identityId={Uri.EscapeDataString(incident.IdentityId)}";
    }

    public static string DetailLink(GovernanceIncident incident)
    {
        return $"/incidents/{Uri.EscapeDataString(incident.Id)}";
    }

    private bool AcceptsHtml()
    {
        return Request.Headers.Accept.ToString().Contains(
            "text/html",
            StringComparison.OrdinalIgnoreCase);
    }
}
