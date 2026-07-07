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
    public async Task GraphView_RendersInteractiveGraphPrototype()
    {
        using var response = await _client.GetAsync("/graph-view");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Interactive Governance Graph", html);
        Assert.Contains("cytoscape.min.js", html);
        Assert.Contains("id=\"interactiveGraph\"", html);
        Assert.Contains("fetch('/graph')", html);
        Assert.Contains("Graph node type legend", html);
        Assert.Contains("legend-capability", html);
        Assert.Contains("legend-identity", html);
        Assert.Contains("legend-policy", html);
        Assert.Contains("legend-resource", html);
        Assert.Contains("selector: 'node.capability'", html);
        Assert.Contains("selector: 'node.identity'", html);
        Assert.Contains("selector: 'node.policy'", html);
        Assert.Contains("selector: 'node.resource'", html);
        Assert.Contains("selector: '.selected'", html);
        Assert.Contains("selector: '.neighbor'", html);
        Assert.Contains("selector: '.connected-edge'", html);
        Assert.Contains("selector: '.dimmed'", html);
        Assert.Contains("cy.on('tap', 'node'", html);
        Assert.Contains("id=\"clearSelectionButton\"", html);
        Assert.Contains("Clear selection", html);
        Assert.Contains("applyNeighborhoodHighlight(node)", html);
        Assert.Contains("clearSelection()", html);
        Assert.Contains("Node Inspector", html);
        Assert.Contains("Select a node to inspect its relationships.", html);
        Assert.Contains("id=\"inspectorMetadata\"", html);
        Assert.Contains("id=\"inspectorConnectedGroups\"", html);
        Assert.Contains("metadata: node.metadata || {}", html);
        Assert.Contains("renderInspector(node)", html);
        Assert.Contains("renderConnectedGroups", html);
        Assert.Contains("Open Interactive Graph", await GetCapabilityExplorerHtml());
    }

    private async Task<string> GetCapabilityExplorerHtml()
    {
        using var response = await _client.GetAsync("/capability-explorer");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }
}
