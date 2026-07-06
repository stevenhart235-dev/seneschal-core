using Microsoft.AspNetCore.Mvc.Testing;

namespace Seneschal.Api.Tests;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _originalCurrentDirectory =
        Directory.GetCurrentDirectory();

    public ApiApplicationFactory()
    {
        var apiProjectDirectory = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Seneschal.Api"));

        Directory.SetCurrentDirectory(apiProjectDirectory);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Directory.SetCurrentDirectory(_originalCurrentDirectory);
    }
}
