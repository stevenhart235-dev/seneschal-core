using System.Net;
using System.Net.Http.Json;
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
        Assert.Equal(
            2,
            root
                .GetProperty("summary")
                .GetProperty("assignedIdentityCount")
                .GetInt32());
        Assert.Equal(
            2,
            root
                .GetProperty("summary")
                .GetProperty("governingPolicyCount")
                .GetInt32());

        var origins = root
            .GetProperty("summary")
            .GetProperty("origins")
            .EnumerateArray()
            .Select(origin => origin.GetInt32())
            .ToArray();

        Assert.Equal([(int)GovernanceRelationshipOrigin.Declared], origins);

        var relationships = root
            .GetProperty("relationships")
            .EnumerateArray()
            .ToList();

        Assert.Equal(4, relationships.Count);
        Assert.Contains(
            relationships,
            relationship =>
                relationship.GetProperty("sourceSystem").GetString() == "PolicyProjection" &&
                relationship.GetProperty("origin").GetInt32() ==
                    (int)GovernanceRelationshipOrigin.Declared);
    }

    [Fact]
    public async Task Overview_UnknownCapabilityReturnsNotFound()
    {
        using var response = await _client.GetAsync(
            "/capabilities/UnknownCapability/overview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Overview_ExposesProjectionWithoutChangingEvaluation()
    {
        using (var overviewResponse = await _client.GetAsync(
            "/capabilities/DeployApplication/overview"))
        {
            Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);

            using var overviewDocument = await ReadJsonAsync(overviewResponse);

            Assert.True(
                overviewDocument.RootElement
                    .GetProperty("relationships")
                    .GetArrayLength() > 0);
        }

        using var evaluateResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "Developer",
                capability = "DeployApplication",
                context = new
                {
                    environment = "dev",
                    resource = "contract-test-resource"
                }
            });

        Assert.Equal(HttpStatusCode.OK, evaluateResponse.StatusCode);

        using var evaluateDocument = await ReadJsonAsync(evaluateResponse);
        var root = evaluateDocument.RootElement;

        Assert.Equal("allow", root.GetProperty("decision").GetString());
        Assert.Equal(
            "Developers can deploy to dev",
            root.GetProperty("policyMatched").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
