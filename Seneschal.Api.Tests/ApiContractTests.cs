using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class ApiContractTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Evaluate_ReturnsCurrentResponseShapeAndDefaultDenyFormatting()
    {
        using var response = await PostEvaluationAsync(
            $"Unknown-{Guid.NewGuid():N}",
            "DeployApplication",
            "dev");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        AssertProperties(
            root,
            "decision",
            "reason",
            "policyMatched",
            "durationMs",
            "effectiveAction",
            "mode");
        Assert.Equal("deny", root.GetProperty("decision").GetString());
        Assert.Equal(
            "logged_only",
            root.GetProperty("effectiveAction").GetString());
        Assert.Equal(
            "default-deny",
            root.GetProperty("policyMatched").GetString());
        Assert.Equal("LogOnly", root.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Evaluate_PreservesRequiresApprovalFormatting()
    {
        using var response = await PostEvaluationAsync(
            "SupportAgent",
            "ReadSecret",
            "prod");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(
            "requires_approval",
            root.GetProperty("decision").GetString());
        Assert.Equal(
            "logged_only",
            root.GetProperty("effectiveAction").GetString());
    }

    [Fact]
    public async Task Policies_ReturnCurrentDtoShape()
    {
        using var response = await _client.GetAsync("/policies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var policy = AssertFirstArrayItem(document.RootElement);

        AssertProperties(
            policy,
            "name",
            "identity",
            "capability",
            "environment",
            "decision",
            "reason");
    }

    [Fact]
    public async Task Capabilities_ReturnCurrentDtoShape()
    {
        using var response = await _client.GetAsync("/capabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var capability = AssertFirstArrayItem(document.RootElement);

        AssertProperties(
            capability,
            "name",
            "description",
            "risk",
            "category");
    }

    [Fact]
    public async Task Identities_ReturnCurrentDtoShape()
    {
        using var response = await _client.GetAsync("/identities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var identity = AssertFirstArrayItem(document.RootElement);

        AssertProperties(
            identity,
            "name",
            "description",
            "type");
    }

    [Fact]
    public async Task Audit_ReturnsCurrentDtoShape()
    {
        var identity = $"Audit-{Guid.NewGuid():N}";

        using (var evaluationResponse = await PostEvaluationAsync(
            identity,
            "DeployApplication",
            "dev"))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var auditEvent = document.RootElement
            .EnumerateArray()
            .Single(item =>
                item.GetProperty("identity").GetString() == identity);

        AssertProperties(
            auditEvent,
            "timestampUtc",
            "identity",
            "capability",
            "context",
            "decision",
            "reason",
            "policyMatched",
            "durationMs");
    }

    private async Task<HttpResponseMessage> PostEvaluationAsync(
        string identity,
        string capability,
        string environment)
    {
        return await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment,
                    resource = "contract-test-resource"
                }
            });
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonElement AssertFirstArrayItem(JsonElement element)
    {
        Assert.Equal(JsonValueKind.Array, element.ValueKind);
        Assert.True(element.GetArrayLength() > 0);
        return element[0];
    }

    private static void AssertProperties(
        JsonElement element,
        params string[] expectedProperties)
    {
        var actualProperties = element
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expectedProperties.Order(StringComparer.Ordinal),
            actualProperties);
    }
}
