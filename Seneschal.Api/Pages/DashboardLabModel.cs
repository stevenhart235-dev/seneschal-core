using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class DashboardLabModel : PageModel
{
    private readonly IActivityStore _activityStore;
    private readonly IAuditEventStore _auditEventStore;
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly IGovernanceModeStore _modeStore;
    private readonly IGovernanceWindowStore _windowStore;

    public DashboardLabModel(
        IActivityStore activityStore,
        IAuditEventStore auditEventStore,
        ICapabilityCatalog capabilityCatalog,
        IGovernanceModeStore modeStore,
        IGovernanceWindowStore windowStore)
    {
        _activityStore = activityStore;
        _auditEventStore = auditEventStore;
        _capabilityCatalog = capabilityCatalog;
        _modeStore = modeStore;
        _windowStore = windowStore;
    }

    public ActivitySnapshot Activity { get; private set; } = new();
    public IReadOnlyList<AuditEvent> Decisions { get; private set; } = [];
    public IReadOnlyList<CapabilityCatalogEntry> Capabilities { get; private set; } = [];
    public GovernanceWindow Window { get; private set; } = null!;
    public EnforcementMode Mode { get; private set; }
    public long Total => Activity.Capabilities.Sum(item => item.TotalRequests);
    public long Allowed => Activity.Capabilities.Sum(item => item.AllowedCount);
    public long Denied => Activity.Capabilities.Sum(item => item.DeniedCount);
    public long Pending => Activity.Capabilities.Sum(item => item.PendingApprovalCount);
    public IReadOnlyList<AuditEvent> InvestigationQueue => Decisions
        .Where(item => item.Decision is DecisionType.Deny or DecisionType.RequireApproval)
        .Take(8).ToList();
    public IReadOnlyList<CapabilityActivity> MostActiveCapabilities => Activity.Capabilities
        .OrderByDescending(item => item.TotalRequests).ThenBy(item => item.CapabilityId)
        .Take(8).ToList();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Activity = await _activityStore.GetSnapshotAsync(cancellationToken);
        Decisions = (await _auditEventStore.GetRecentAsync(100, cancellationToken))
            .OrderByDescending(item => item.TimestampUtc).ToList();
        Capabilities = (await _capabilityCatalog.SearchAsync(
            new CapabilityCatalogQuery(), cancellationToken)).ToList();
        Mode = _modeStore.GetMode();
        Window = _windowStore.GetWindow();
    }

    public static string DecisionLabel(DecisionType decision) =>
        decision == DecisionType.RequireApproval ? "Pending Approval" : decision.ToString();

    public static string BadgeClass(DecisionType decision) => decision switch
    {
        DecisionType.Allow => "bg-green-lt text-green",
        DecisionType.Deny => "bg-red-lt text-red",
        _ => "bg-yellow-lt text-yellow"
    };

    public static string Age(DateTimeOffset timestamp)
    {
        var age = DateTimeOffset.UtcNow - timestamp;
        return age.TotalMinutes < 1 ? "just now" : age.TotalHours < 1
            ? $"{Math.Max(1, (int)age.TotalMinutes)}m ago"
            : $"{Math.Max(1, (int)age.TotalHours)}h ago";
    }
}
