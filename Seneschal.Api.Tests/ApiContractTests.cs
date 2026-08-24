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
            "mode",
            "executionGuidance",
            "approvalId",
            "approvalStatus",
            "operationId",
            "approvalCorrelationMode",
            "message",
            "retryGuidance");
        Assert.Equal("deny", root.GetProperty("decision").GetString());
        Assert.Equal(
            "logged_only",
            root.GetProperty("effectiveAction").GetString());
        Assert.Equal(
            "default-deny",
            root.GetProperty("policyMatched").GetString());
        Assert.Equal("LogOnly", root.GetProperty("mode").GetString());
        Assert.Equal("ContinueLogOnly", root.GetProperty("executionGuidance").GetString());
        Assert.False(root.TryGetProperty("shouldProceed", out _));
    }

    [Fact]
    public async Task Evaluate_PreservesRequiresApprovalFormatting()
    {
        using var response = await PostEvaluationAsync(
            "SupportAgent",
            "azure.keyvault.secret.read",
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
        Assert.Equal("ContinueLogOnly", root.GetProperty("executionGuidance").GetString());
        Assert.False(root.TryGetProperty("shouldProceed", out _));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("approvalId").GetString()));
        Assert.Equal("Pending", root.GetProperty("approvalStatus").GetString());
        Assert.Equal("LegacyContext", root.GetProperty("approvalCorrelationMode").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("operationId").ValueKind);
    }

    [Fact]
    public async Task Evaluate_RoundTripsCallerOwnedOperationIdForApproval()
    {
        using var response = await _client.PostAsJsonAsync("/evaluate", new
        {
            identity = "SupportAgent",
            capability = "azure.keyvault.secret.read",
            operationId = "release-001",
            context = new { environment = "prod", resource = "vault" }
        });
        using var document = await ReadJsonAsync(response);
        Assert.Equal("release-001", document.RootElement.GetProperty("operationId").GetString());
        Assert.Equal("Operation", document.RootElement.GetProperty("approvalCorrelationMode").GetString());
    }

    [Fact]
    public async Task Evaluate_ResponseIsCompatibleWithTypedClientContract()
    {
        var client = Seneschal.Client.SeneschalClient.Create(
            _client,
            _client.BaseAddress ?? throw new InvalidOperationException(
                "The test client requires a base address."));

        var result = await client.EvaluateAsync(new Seneschal.Client.Models.DecisionRequest
        {
            Identity = $"TypedClient-{Guid.NewGuid():N}",
            Capability = "DeployApplication",
            Context = new Dictionary<string, string>
            {
                ["environment"] = "dev",
                ["resource"] = "typed-client-contract"
            }
        });

        Assert.Equal("ContinueLogOnly", result.RawExecutionGuidance);
        Assert.Equal(
            Seneschal.Client.ExecutionGuidanceKind.ContinueLogOnly,
            result.Guidance);
        Assert.True(result.ShouldProceed);
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
            "displayName",
            "description",
            "owner",
            "severity",
            "rationale",
            "identity",
            "identities",
            "capability",
            "capabilities",
            "environment",
            "environments",
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
            "displayName",
            "description",
            "risk",
            "category",
            "owner",
            "lifecycle",
            "documentationUrl",
            "tags",
            "technology");
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
            "displayName",
            "owner",
            "application",
            "environment",
            "technology",
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
                item.GetProperty("identityId").GetString() == identity);

        AssertProperties(
            auditEvent,
            "id",
            "requestId",
            "timestampUtc",
            "identityId",
            "capabilityId",
            "resourceId",
            "environment",
            "decision",
            "enforcementMode",
            "matchedPolicies",
            "obligations",
            "reason",
            "evaluationDurationMs",
            "governanceWindowName",
            "governanceWindowMode",
            "governanceWindowMessage",
            "governanceWindowReason",
            "policyDecision",
            "policyReason",
            "policyEvaluations",
            "approvalId",
            "approvalStatus",
            "approvalAction",
            "approvalRequestReason",
            "approvalResolvedAt",
            "approvalResolvedBy",
            "approvalConsumedAt",
            "approvalConsumedByDecisionId",
            "approvalOperationId",
            "approvalCorrelationMode",
            "governanceConfigurationFingerprint",
            "executionGuidance",
            "callerMessage",
            "retryGuidance");
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
