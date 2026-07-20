using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PortalRoutingTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public PortalRoutingTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_RedirectsToDashboard()
    {
        using var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/dashboard")).StatusCode);
    }

    [Theory]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("/monitor", "/monitor")]
    [InlineData("/governance", "/governance")]
    [InlineData("/governance-windows", "/governance-windows")]
    [InlineData("/policies", "/policies")]
    [InlineData("/capability-explorer", "/capability-explorer")]
    [InlineData("/identity-explorer", "/identity-explorer")]
    [InlineData("/capability-activity", "/capability-activity")]
    [InlineData("/identity-activity", "/identity-activity")]
    [InlineData("/audit", "/audit")]
    [InlineData("/incidents", "/incidents")]
    [InlineData("/graph-view", "/graph-view")]
    [InlineData("/developer-quickstart", "/developer-quickstart")]
    [InlineData("/diagnostics-view", "/diagnostics-view")]
    public async Task SidebarRoute_RendersSharedPortalShell(
        string route,
        string activeHref)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div class=\"app-shell\">", html);
        Assert.Contains("<aside class=\"sidebar\">", html);
        Assert.Contains("Capability control plane", html);
        Assert.Contains("href=\"/identity-explorer\"", html);
        Assert.Contains("href=\"/diagnostics-view\"", html);
        Assert.Contains($"href=\"{activeHref}\"", html);
        Assert.Contains("aria-current=\"page\"", html);
    }

    [Fact]
    public async Task Identities_RendersConfiguredIdentityHtmlAndActivityLinks()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/identity-explorer");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<h1>Identities</h1>", html);
        Assert.Contains("PlatformEngineer", html);
        Assert.Contains("Platform engineering operator", html);
        Assert.Contains("/identity-activity?identityId=PlatformEngineer", html);
        Assert.False(html.TrimStart().StartsWith("[", StringComparison.Ordinal));
        Assert.Contains("class=\"active\"", html);
        Assert.Contains("href=\"/identity-explorer\"", html);
    }

    [Fact]
    public async Task MachineReadableIdentityAndDiagnosticsEndpointsRemainJson()
    {
        using var client = _factory.CreateClient();

        using var identities = await client.GetAsync("/identities");
        using var diagnostics = await client.GetAsync("/diagnostics");

        Assert.Equal("application/json", identities.Content.Headers.ContentType?.MediaType);
        Assert.Equal("application/json", diagnostics.Content.Headers.ContentType?.MediaType);
        using var identitiesJson = JsonDocument.Parse(await identities.Content.ReadAsStringAsync());
        using var diagnosticsJson = JsonDocument.Parse(await diagnostics.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, identitiesJson.RootElement.ValueKind);
        Assert.Equal(JsonValueKind.Object, diagnosticsJson.RootElement.ValueKind);
    }

    [Theory]
    [InlineData("/dashboard", "class=\"active\" href=\"/dashboard\"")]
    [InlineData("/policies", "class=\"active\" href=\"/policies\" aria-current=\"page\"")]
    [InlineData("/audit", "class=\"active\" href=\"/audit\" aria-current=\"page\"")]
    [InlineData("/diagnostics-view", "class=\"active\" href=\"/diagnostics-view\" aria-current=\"page\"")]
    [InlineData("/developer-quickstart", "class=\"active\" href=\"/developer-quickstart\" aria-current=\"page\"")]
    [InlineData("/governance-windows", "class=\"active\" href=\"/governance-windows\" aria-current=\"page\"")]
    public async Task RepresentativePages_MarkCorrectNavigationItemActive(
        string route,
        string expectedMarkup)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(expectedMarkup, html);
    }
}
