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

    [Fact]
    public async Task Graph_IncludesCapabilityCatalogMetadata()
    {
        using var response = await _client.GetAsync("/graph");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var capability = document.RootElement
            .GetProperty("nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("id").GetString() ==
                "capability:DeployApplication");
        var metadata = capability.GetProperty("metadata");

        Assert.Equal("Deploy Application", capability.GetProperty("label").GetString());
        Assert.Equal("Platform Engineering", metadata.GetProperty("owner").GetString());
        Assert.Equal("Medium", metadata.GetProperty("riskLevel").GetString());
        Assert.Equal("Deployment", metadata.GetProperty("category").GetString());
        Assert.Equal("Active", metadata.GetProperty("lifecycle").GetString());
        Assert.Contains("legacy-sample", metadata.GetProperty("tags").GetString());
        Assert.StartsWith("https://", metadata.GetProperty("documentationUrl").GetString());
    }

    [Fact]
    public async Task Graph_UsesOnlySupportedRelationshipNodeTypes()
    {
        using var response = await _client.GetAsync("/graph");
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        var nodeTypes = document.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .Select(node => node.GetProperty("type").GetString())
            .ToHashSet();

        Assert.Contains("Capability", nodeTypes);
        Assert.Contains("Identity", nodeTypes);
        Assert.Contains("Policy", nodeTypes);
        Assert.Contains("Resource", nodeTypes);
        Assert.Subset(
            new HashSet<string?> { "Capability", "Identity", "Policy", "Resource" },
            nodeTypes);
    }
}
