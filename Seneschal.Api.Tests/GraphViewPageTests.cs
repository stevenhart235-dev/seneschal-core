using System.Net;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GraphViewPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public GraphViewPageTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphView_RendersCapabilityCenteredD3Explorer()
    {
        using var response = await _client.GetAsync("/graph-view");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capability Relationship Graph", html);
        Assert.Contains("/vendor/d3/d3.v7.9.0.min.js", html);
        Assert.DoesNotContain("https://unpkg.com", html);
        Assert.Contains("id=\"interactiveGraph\"", html);
        Assert.Contains("data-capability-id=", html);
        Assert.Contains("/graph-view.js", html);
        Assert.Contains("Graph node type legend", html);
        Assert.Contains("legend-capability", html);
        Assert.Contains("legend-identity", html);
        Assert.Contains("legend-policy", html);
        Assert.Contains("legend-resource", html);
        Assert.Contains("id=\"resetGraphButton\"", html);
        Assert.Contains("id=\"fitGraphButton\"", html);
        Assert.Contains("Node details", html);
        Assert.Contains("Select a node to inspect it.", html);
        Assert.Contains("id=\"inspectorMetadata\"", html);
        Assert.Contains("id=\"inspectorLinks\"", html);
        Assert.Contains("Relationship list", html);
        Assert.Contains("Accessible non-graph view", html);
        Assert.Contains("Open Interactive Graph", await GetCapabilityExplorerHtml());
    }

    [Fact]
    public async Task GraphView_UnknownCapabilityRendersSafeEmptyState()
    {
        using var response = await _client.GetAsync(
            "/graph-view?capabilityId=unknown.capability");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("No capability graph available", html);
        Assert.DoesNotContain("id=\"interactiveGraph\"", html);
    }

    [Fact]
    public async Task LocalD3Asset_IsServedByTheApplication()
    {
        using var response = await _client.GetAsync(
            "/vendor/d3/d3.v7.9.0.min.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("d3", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GraphScript_UsesD3InteractionsAndCapabilityCentering()
    {
        using var response = await _client.GetAsync("/graph-view.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var script = await response.Content.ReadAsStringAsync();
        Assert.Contains("d3.forceSimulation", script);
        Assert.Contains("source: edge.sourceId", script);
        Assert.Contains("target: edge.targetId", script);
        Assert.Contains("d3.zoom()", script);
        Assert.Contains("d3.drag()", script);
        Assert.Contains("center.fx = width / 2", script);
        Assert.Contains("fitToGraph", script);
        Assert.Contains("resetGraphButton", script);
        Assert.Contains("encodeURIComponent(domainId)", script);
    }

    private async Task<string> GetCapabilityExplorerHtml()
    {
        using var response = await _client.GetAsync("/capability-explorer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }
}
