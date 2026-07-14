using System.Net;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DeveloperQuickstartPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public DeveloperQuickstartPageTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Page_RendersIntegrationsCapabilitiesAndSharedShell()
    {
        using var response = await _client.GetAsync("/developer-quickstart");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<h1>Developer Quickstart</h1>", html);
        Assert.Contains("ASP.NET Core", html);
        Assert.Contains(".NET Client", html);
        Assert.Contains("GitHub Actions", html);
        Assert.Contains("Terraform / OpenTofu", html);
        Assert.Contains("production.deployment.execute", html);
        Assert.Contains("infrastructure.production.apply", html);
        Assert.Contains("class=\"active\"", html);
        Assert.Contains("href=\"/developer-quickstart\"", html);
    }

    [Fact]
    public async Task Page_MasksCheckedInDevelopmentKeysByDefault()
    {
        var html = await _client.GetStringAsync("/developer-quickstart");

        Assert.Contains("Development/demo only", html);
        Assert.Contains("id=\"quickstartMaskedKey\"", html);
        Assert.Contains("••••••••••••••••••••••••", html);
        Assert.Contains("id=\"revealQuickstartKey\"", html);
        Assert.Contains(ApiApplicationFactory.TestApiKey, html);
        Assert.DoesNotContain("production-api-key", html);
    }

    [Fact]
    public async Task Page_UsesLocalScriptAndLinksRepositoryDocumentation()
    {
        var html = await _client.GetStringAsync("/developer-quickstart");

        Assert.Contains("src=\"/developer-quickstart.js\"", html);
        Assert.Contains("id=\"quickstartDocsLink\"", html);

        var script = await _client.GetStringAsync("/developer-quickstart.js");
        Assert.Contains("Seneschal.AspNetCore --version 0.1.0-alpha.1", script);
        Assert.Contains("Seneschal.Client --version 0.1.0-alpha.1", script);
        Assert.Contains("invoke-seneschal-gate.ps1", script);
        Assert.Contains("quickstartCapability", script);
        Assert.Contains("navigator.clipboard", script);
        Assert.Contains("keyRevealed", script);
        Assert.Contains("docs/quickstart/aspnet-core-quickstart.md", script);
        Assert.DoesNotContain("apiKey: 'dev-", script);
    }
}
