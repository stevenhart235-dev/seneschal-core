using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GovernanceWindowsPageTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public GovernanceWindowsPageTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Page_RendersBuiltInProductionFreezeAndAffectedCapabilities()
    {
        using var client = CreateIsolatedClient();
        using var response = await client.GetAsync("/governance-windows");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Governance Windows</h1>", html);
        Assert.Contains("Production Freeze", html);
        Assert.Contains("Weekend production freeze.", html);
        Assert.Contains("production.deployment.execute", html);
        Assert.Contains("infrastructure.production.apply", html);
        Assert.Contains("infrastructure.production.destroy", html);
        Assert.Contains("Temporary runtime state", html);
    }

    [Fact]
    public async Task Post_EnablesEnforceModeForFutureEvaluations()
    {
        using var client = CreateIsolatedClient();
        using var content = new FormUrlEncodedContent(
        [
            new("enabled", "true"),
            new("mode", "Enforce")
        ]);

        using var response = await client.PostAsync("/governance-windows?handler=SetState", content);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("window-enforce", html);
        Assert.Contains("<dd>Yes</dd>", html);
        Assert.Contains("<dd>Enforce</dd>", html);
    }

    private HttpClient CreateIsolatedClient() => _factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGovernanceWindowStore>();
            services.AddSingleton<IGovernanceWindowStore, InMemoryGovernanceWindowStore>();
        });
    }).CreateClient();
}
