using System.Net;
using System.Text.Json;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityOverviewEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public CapabilityOverviewEndpointTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Overview_ExistingCapabilityReturnsReadModel()
    {
        using var response = await _client.GetAsync(
            "/capabilities/DeployApplication/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;
        var capability = root
            .GetProperty("catalogEntry")
            .GetProperty("capability");

        Assert.Equal(
            "DeployApplication",
            capability.GetProperty("id").GetString());
        Assert.Equal(
            "DeployApplication",
            capability.GetProperty("name").GetString());
        Assert.Equal(
            "Deployment",
            capability.GetProperty("category").GetString());
        Assert.Equal(
            (int)RiskLevel.Medium,
            capability.GetProperty("riskLevel").GetInt32());
        Assert.Empty(root.GetProperty("relationships").EnumerateArray());
        Assert.Equal(
            0,
            root
                .GetProperty("summary")
                .GetProperty("assignedIdentityCount")
                .GetInt32());
    }

    [Fact]
    public async Task Overview_UnknownCapabilityReturnsNotFound()
    {
        using var response = await _client.GetAsync(
            "/capabilities/UnknownCapability/overview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
