using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ActivityEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public ActivityEndpointTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Activity_ReturnsSnapshotShape()
    {
        using var response = await _client.GetAsync("/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("capabilities", out var capabilities));
        Assert.True(root.TryGetProperty("identities", out var identities));
        Assert.True(root.TryGetProperty("policies", out var policies));
        Assert.Equal(JsonValueKind.Array, capabilities.ValueKind);
        Assert.Equal(JsonValueKind.Array, identities.ValueKind);
        Assert.Equal(JsonValueKind.Array, policies.ValueKind);
    }

    [Fact]
    public async Task Evaluate_UpdatesActivityMetricsAndPreservesAudit()
    {
        var identity = $"Activity-{Guid.NewGuid():N}";
        var capability = $"ActivityCapability-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "activity-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var activityResponse = await _client.GetAsync("/activity");
        using var auditResponse = await _client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, activityResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        using var activityDocument = await ReadJsonAsync(activityResponse);
        using var auditDocument = await ReadJsonAsync(auditResponse);

        var capabilityActivity = activityDocument.RootElement
            .GetProperty("capabilities")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("capabilityId").GetString() == capability);
        var identityActivity = activityDocument.RootElement
            .GetProperty("identities")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("identityId").GetString() == identity);
        var policyActivity = activityDocument.RootElement
            .GetProperty("policies")
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("policyId").GetString() == "default-deny");
        var auditEvent = auditDocument.RootElement
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("identityId").GetString() == identity);

        Assert.Equal(1, capabilityActivity.GetProperty("totalRequests").GetInt64());
        Assert.Equal(1, capabilityActivity.GetProperty("deniedCount").GetInt64());
        Assert.True(
            capabilityActivity
                .GetProperty("averageEvaluationDurationMs")
                .GetDouble() >= 0);
        Assert.Equal(1, identityActivity.GetProperty("totalRequests").GetInt64());
        Assert.Contains(
            capability,
            identityActivity
                .GetProperty("distinctCapabilitiesUsed")
                .EnumerateArray()
                .Select(item => item.GetString()));
        Assert.True(policyActivity.GetProperty("matchCount").GetInt64() >= 1);
        Assert.Equal(capability, auditEvent.GetProperty("capabilityId").GetString());
        Assert.Equal("default-deny", auditEvent
            .GetProperty("matchedPolicies")
            .EnumerateArray()
            .Single()
            .GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
