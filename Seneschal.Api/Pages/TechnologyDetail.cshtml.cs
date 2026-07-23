using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;

namespace Seneschal.Api.Pages;

public sealed class TechnologyDetailModel : PageModel
{
    private readonly TechnologyActivityService _technologyService;
    public TechnologyDetailModel(TechnologyActivityService technologyService) => _technologyService = technologyService;
    public TechnologyActivity Technology { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string technologyKey, CancellationToken cancellationToken)
    {
        var technology = await _technologyService.GetTechnologyAsync(technologyKey, cancellationToken);
        if (technology is null) return NotFound();
        Technology = technology;
        return Page();
    }
}
