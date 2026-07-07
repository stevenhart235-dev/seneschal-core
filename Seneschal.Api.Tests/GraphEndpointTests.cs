using System.Net;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GraphEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public GraphEndpointTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Graph_ReturnsGraphDataJson()
    {
        using var response = await _client.GetAsync("/graph");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("nodes", out var nodes));
        Assert.True(root.TryGetProperty("edges", out var edges));
        Assert.Equal(JsonValueKind.Array, nodes.ValueKind);
        Assert.Equal(JsonValueKind.Array, edges.ValueKind);
        Assert.True(nodes.GetArrayLength() > 0);
        Assert.True(edges.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Graph_IncludesProjectedPolicyRelationships()
    {
        using var response = await _client.GetAsync("/graph");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        var nodes = document.RootElement.GetProperty("nodes");
        var edges = document.RootElement.GetProperty("edges");

        Assert.Contains(
            nodes.EnumerateArray(),
            node => node.GetProperty("id").GetString() ==
                    "capability:DeployApplication" &&
                node.GetProperty("type").GetString() == "Capability");
        Assert.Contains(
            nodes.EnumerateArray(),
            node => node.GetProperty("id").GetString() ==
                    "identity:Developer" &&
                node.GetProperty("type").GetString() == "Identity");
        Assert.Contains(
            edges.EnumerateArray(),
            edge => edge.GetProperty("sourceId").GetString() ==
                    "policy:Developers can deploy to dev" &&
                edge.GetProperty("targetId").GetString() ==
                    "capability:DeployApplication" &&
                edge.GetProperty("relationshipType").GetString() ==
                    "PolicyAppliesToCapability");
    }
}
