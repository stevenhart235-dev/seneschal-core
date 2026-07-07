using System.Net;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityExplorerPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public CapabilityExplorerPageTests(ApiApplicationFactory factory)
    {
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
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Capability Metadata", html);
        Assert.Contains("Governance Summary", html);
        Assert.Contains("Assigned Identities", html);
        Assert.Contains("Governing Policies", html);
        Assert.Contains("PolicyProjection", html);
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
    public async Task CapabilityExplorer_SelectingSearchResultRendersOverview()
    {
        using var response = await _client.GetAsync(
            "/capability-explorer?q=secret&capabilityId=azure.keyvault.secret.read");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Search Results", html);
        Assert.Contains("Capability Metadata", html);
        Assert.Contains("azure.keyvault.secret.read", html);
        Assert.Contains("Support secret reads require approval", html);
    }
}
