using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;

namespace Seneschal.Api.Pages;

[IgnoreAntiforgeryToken]
public sealed class GovernanceModel : PageModel
{
    private readonly IGovernanceModeStore _governanceModeStore;

    public GovernanceModel(IGovernanceModeStore governanceModeStore)
    {
        _governanceModeStore = governanceModeStore;
    }

    public EnforcementMode CurrentMode { get; private set; }

    public string CurrentModeLabel => CurrentMode == EnforcementMode.LogOnly
        ? "LogOnly"
        : "Enforce";

    public string CurrentModeDescription => CurrentMode == EnforcementMode.LogOnly
        ? "Policies are evaluated and audited, but deny and pending approval decisions are projected as logged-only."
        : "Deny and pending approval decisions may block integrated applications.";

    public void OnGet()
    {
        CurrentMode = _governanceModeStore.GetMode();
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
}
