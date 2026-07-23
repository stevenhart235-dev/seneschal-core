using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Services;

namespace Seneschal.Api.Pages;

public sealed class TechnologiesModel : PageModel
{
    private readonly TechnologyActivityService _technologyService;
    public TechnologiesModel(TechnologyActivityService technologyService) => _technologyService = technologyService;
    public IReadOnlyList<TechnologyActivity> Technologies { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Technologies = await _technologyService.GetTechnologiesAsync(cancellationToken);
}
