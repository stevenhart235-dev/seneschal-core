using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class HealthDiagnosticsEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthDiagnosticsEndpointTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthyJson()
    {
        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Live_ReturnsLiveJson()
    {
        using var response = await _client.GetAsync("/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("live", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Ready_ReturnsReadinessDetails()
    {
        using var response = await _client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("ready", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("capabilitiesLoaded").GetBoolean());
        Assert.True(root.GetProperty("identitiesLoaded").GetBoolean());
        Assert.True(root.GetProperty("policiesLoaded").GetBoolean());
        Assert.True(root.GetProperty("runtimeSettingsLoaded").GetBoolean());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Diagnostics_ReturnsCountsAndComponentTypes()
    {
        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = $"Diagnostics-{Guid.NewGuid():N}",
                capability = $"DiagnosticsCapability-{Guid.NewGuid():N}",
                context = new
                {
                    environment = "dev",
                    resource = "diagnostics-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("LogOnly", root.GetProperty("currentRuntimeMode").GetString());
        Assert.True(root.GetProperty("capabilityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("identityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("policyCount").GetInt32() > 0);
        Assert.True(root.GetProperty("auditEventCount").GetInt32() > 0);
        Assert.True(root.GetProperty("activityCapabilityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("activityIdentityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("activityPolicyCount").GetInt32() > 0);
        Assert.Equal("NullDecisionExporter", root.GetProperty("exporterType").GetString());
        Assert.Equal("InMemoryDecisionMetrics", root.GetProperty("metricsType").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Diagnostics_DoesNotExposeRawPolicyContentsOrSecrets()
    {
        using var response = await _client.GetAsync("/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            "Developer is allowed to deploy applications to dev",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Reading production secrets requires approval",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Developers can deploy to dev",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Support secret reads require approval",
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
