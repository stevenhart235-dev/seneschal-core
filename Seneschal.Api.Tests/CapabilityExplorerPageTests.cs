using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityExplorerPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public CapabilityExplorerPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CapabilityExplorer_RendersKnownCapabilityOverview()
    {
        using var response = await _client.GetAsync(
            "/capability-explorer?capabilityId=DeployApplication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Seneschal Capability Explorer", html);
        Assert.Contains("Dashboard", html);
        Assert.Contains("Capabilities", html);
        Assert.Contains("class=\"active\"", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Capability Profile", html);
        Assert.Contains("Runtime Summary", html);
        Assert.Contains("badge risk-badge risk-medium", html);
        Assert.Contains("Governance Relationships", html);
        Assert.Contains("Governance Summary", html);
        Assert.Contains("Assigned Identities", html);
        Assert.Contains("Governing Policies", html);
        Assert.Contains("relationship-compact-grid", html);
        Assert.Contains("relationship-compact-group", html);
        Assert.Contains("relationship-chip", html);
        Assert.Contains(">Developer</span>", html);
        Assert.Contains(">Developers can deploy to dev</span>", html);
        Assert.Contains("PolicyProjection", html);
        Assert.Contains("Declared · PolicyProjection", html);
        Assert.DoesNotContain("Origin:", html);
        Assert.DoesNotContain("Source:", html);
        Assert.Contains("Graph View", html);
        Assert.Contains("aria-label=\"Capability ego graph\"", html);
        Assert.Contains("graph-node graph-node-capability", html);
        Assert.Contains("Related Identities", html);
        Assert.Contains("Related Policies", html);
        Assert.Contains("Recent Decisions", html);
        Assert.Contains("/audit?capabilityId=DeployApplication", html);
        Assert.Contains("Recommendations", html);
        Assert.Contains("/monitor", html);
        Assert.Contains("Seneschal v0.2.1-alpha", html);
    }

    [Fact]
    public async Task CapabilityExplorer_RendersRuntimeEmptyState()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.RemoveAll<IAuditEventStore>();
                    services.RemoveAll<IAuditSink>();
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                    services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
                    services.AddSingleton<IAuditSink>(
                        services => services.GetRequiredService<IAuditEventStore>());
                });
            })
            .CreateClient();

        using var response = await client.GetAsync(
            "/capability-explorer?capabilityId=DeployApplication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No runtime activity has been observed", html);
        Assert.Contains("Observe runtime activity before enforcing", html);
    }

    [Fact]
    public async Task CapabilityExplorer_RendersRuntimeSummaryAndRecentDecisions()
    {
        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "Developer",
                capability = "DeployApplication",
                context = new
                {
                    environment = "dev",
                    resource = "capability-profile-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync(
            "/capability-explorer?capabilityId=DeployApplication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Runtime Summary", html);
        Assert.Contains("Total requests", html);
        Assert.Contains("Allowed", html);
        Assert.Contains("Last used", html);
        Assert.Contains("Avg duration ms", html);
        Assert.Contains("Recent Decisions", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Allow", html);
        Assert.Contains("Review enforcement readiness", html);
    }

    [Fact]
    public async Task CapabilityExplorer_SearchRendersMatchingCapabilities()
    {
        using var response = await _client.GetAsync(
            "/capability-explorer?q=secret");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Search Results", html);
        Assert.Contains("azure.keyvault.secret.read", html);
        Assert.Contains("Read a production secret", html);
    }

    [Fact]
    public async Task CapabilityExplorer_SearchMissExplainsCatalogSearch()
    {
        using var response = await _client.GetAsync(
            "/capability-explorer?q=missing-capability");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No capabilities matched", html);
        Assert.Contains(
            "The Capability Explorer searches the configured",
            html);
        Assert.Contains("azure.keyvault.secret.read", html);
    }

    [Fact]
    public async Task CapabilityExplorer_SelectingSearchResultRendersOverview()
    {
        using var response = await _client.GetAsync(
            "/capability-explorer?q=secret&capabilityId=azure.keyvault.secret.read");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Search Results", html);
        Assert.Contains("Capability Profile", html);
        Assert.Contains("azure.keyvault.secret.read", html);
        Assert.Contains("Support secret reads require approval", html);
    }

    [Fact]
    public async Task CapabilityExplorer_UnknownCapabilityExplainsCatalogSource()
    {
        using var response = await _client.GetAsync(
            "/capability-explorer?capabilityId=unknown-capability");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capability not found", html);
        Assert.Contains(
            "Capability pages are created from catalog entries",
            html);
        Assert.Contains("azure.keyvault.secret.read", html);
    }
}
