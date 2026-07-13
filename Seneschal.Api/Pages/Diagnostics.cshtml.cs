using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;

namespace Seneschal.Api.Pages;

public sealed class DiagnosticsModel(
    IGovernanceModeStore governanceModeStore,
    CapabilityLoader capabilityLoader,
    IdentityLoader identityLoader,
    PolicyLoader policyLoader,
    IAuditEventStore auditEventStore) : PageModel
{
    public string RuntimeMode { get; private set; } = string.Empty;
    public int CapabilityCount { get; private set; }
    public int IdentityCount { get; private set; }
    public int PolicyCount { get; private set; }
    public int AuditEventCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        RuntimeMode = governanceModeStore.GetMode().ToString();
        CapabilityCount = capabilityLoader.GetCapabilities().Count;
        IdentityCount = identityLoader.GetIdentities().Count;
        PolicyCount = policyLoader.GetPolicies().Count;
        AuditEventCount = (await auditEventStore.GetRecentAsync(
            count: int.MaxValue,
            cancellationToken: cancellationToken)).Count;
    }
}
