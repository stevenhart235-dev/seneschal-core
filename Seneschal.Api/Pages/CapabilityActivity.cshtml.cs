using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class CapabilityActivityModel : PageModel
{
    private readonly IActivityStore _activityStore;

    public CapabilityActivityModel(IActivityStore activityStore)
    {
        _activityStore = activityStore;
    }

    public string? CapabilityId { get; private set; }
    public IReadOnlyCollection<CapabilityActivity> Capabilities { get; private set; }
        = [];
    public CapabilityActivity? SelectedCapability { get; private set; }
    public bool CapabilityWasRequested => !string.IsNullOrWhiteSpace(CapabilityId);
    public bool HasActivity => Capabilities.Count > 0;

    public async Task OnGetAsync(
        string? capabilityId,
        CancellationToken cancellationToken)
    {
        CapabilityId = capabilityId;
        var snapshot = await _activityStore.GetSnapshotAsync(cancellationToken);

        Capabilities = snapshot.Capabilities
            .OrderByDescending(capability => capability.TotalRequests)
            .ThenByDescending(capability => capability.DeniedCount)
            .ThenByDescending(capability => capability.PendingApprovalCount)
            .ThenBy(capability => capability.CapabilityId)
            .ToList();

        if (!string.IsNullOrWhiteSpace(capabilityId))
        {
            SelectedCapability = Capabilities.FirstOrDefault(capability =>
                string.Equals(
                    capability.CapabilityId,
                    capabilityId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
