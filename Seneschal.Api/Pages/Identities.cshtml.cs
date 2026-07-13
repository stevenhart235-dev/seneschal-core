using Microsoft.AspNetCore.Mvc.RazorPages;
using Seneschal.Api.Models;
using Seneschal.Api.Services;

namespace Seneschal.Api.Pages;

public sealed class IdentitiesModel(IdentityLoader identityLoader) : PageModel
{
    public IReadOnlyList<IdentityDefinition> Identities { get; private set; } = [];

    public void OnGet()
    {
        Identities = identityLoader.GetIdentities()
            .OrderBy(identity => identity.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
