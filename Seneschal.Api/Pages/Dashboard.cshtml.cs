using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

public sealed class DashboardModel : PageModel
{
    private readonly ICapabilityCatalog _capabilityCatalog;
    private readonly IGovernanceGraph _governanceGraph;
    private readonly IdentityLoader _identityLoader;
    private readonly PolicyLoader _policyLoader;

    public DashboardModel(
        ICapabilityCatalog capabilityCatalog,
        IGovernanceGraph governanceGraph,
        IdentityLoader identityLoader,
        PolicyLoader policyLoader)
    {
        _capabilityCatalog = capabilityCatalog;
        _governanceGraph = governanceGraph;
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

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var capabilities = await _capabilityCatalog.SearchAsync(
            new CapabilityCatalogQuery(),
            cancellationToken);
        var relationships = await _governanceGraph.QueryAsync(
            new GovernanceRelationshipQuery(),
            cancellationToken);

        TotalCapabilities = capabilities.Count;
        TotalPolicies = _policyLoader.GetPolicies().Count;
        TotalIdentities = _identityLoader.GetIdentities().Count;
        TotalRelationships = relationships.Count;

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
