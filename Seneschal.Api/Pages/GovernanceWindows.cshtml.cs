using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Pages;

[IgnoreAntiforgeryToken]
public sealed class GovernanceWindowsModel : PageModel
{
    private readonly IGovernanceWindowStore _windowStore;
    private readonly ICapabilityCatalog _capabilityCatalog;

    public GovernanceWindowsModel(
        IGovernanceWindowStore windowStore,
        ICapabilityCatalog capabilityCatalog)
    {
        _windowStore = windowStore;
        _capabilityCatalog = capabilityCatalog;
    }

    public GovernanceWindow Window { get; private set; } = null!;
    public IReadOnlyCollection<CapabilityCatalogEntry> Capabilities
        { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Window = _windowStore.GetWindow();
        Capabilities = (await _capabilityCatalog.SearchAsync(
                new CapabilityCatalogQuery(),
                cancellationToken))
            .OrderBy(entry => entry.Capability.DisplayName)
            .ThenBy(entry => entry.Capability.Id)
            .ToList();
    }

    public IActionResult OnPostSetState(bool enabled, string mode)
    {
        if (!Enum.TryParse<GovernanceWindowMode>(
                mode,
                ignoreCase: true,
                out var parsedMode))
        {
            return BadRequest();
        }

        _windowStore.SetState(enabled, parsedMode);
        return RedirectToPage();
    }
}
