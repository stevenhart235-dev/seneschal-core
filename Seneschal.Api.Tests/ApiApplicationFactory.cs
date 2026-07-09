using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Models;
using Seneschal.Api.Services;

namespace Seneschal.Api.Tests;

public sealed class ApiApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "dev-test-key";

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IntegrationApiKeyLoader>();
            services.AddSingleton(new IntegrationApiKeyLoader(
            [
                new IntegrationApiKey
                {
                    Name = "test-wildcard",
                    Key = TestApiKey,
                    Enabled = true,
                    AllowedIdentities = ["*"],
                    AllowedCapabilities = ["*"]
                }
            ]));
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(
            IntegrationApiKeyAuthorizer.HeaderName,
            TestApiKey);
    }
}
