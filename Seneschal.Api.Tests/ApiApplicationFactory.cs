using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Models;
using Seneschal.Api.Services;

namespace Seneschal.Api.Tests;

public class ApiApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestApiKey = "dev-test-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var apiProjectDirectory = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Seneschal.Api"));

        builder.UseContentRoot(apiProjectDirectory);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IntegrationApiKeyLoader>();
            services.AddSingleton(IntegrationApiKeyLoader.FromKeys(
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
