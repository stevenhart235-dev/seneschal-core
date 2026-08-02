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

    public async Task<IActionResult> OnPostSetStateAsync(
        bool enabled, string mode, long? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<GovernanceWindowMode>(
                mode,
                ignoreCase: true,
                out var parsedMode))
        {
            return BadRequest();
        }

        try
        {
            await _windowStore.SetStateAsync(enabled, parsedMode,
                expectedVersion ?? _windowStore.GetWindow().Version,
                reason: "Governance Window state changed through the operator portal.",
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
                "The Governance Window change could not be persisted. Retry the request.");
        }
        return RedirectToPage();
    }
}
