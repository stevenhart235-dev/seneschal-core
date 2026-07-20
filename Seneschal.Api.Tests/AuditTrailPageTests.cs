using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class AuditTrailPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditTrailPageTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Audit_WithHtmlAcceptHeaderRendersAuditTrail()
    {
        await PostEvaluationAsync(
            "Developer",
            "DeployApplication",
            "dev");
        await PostEvaluationAsync(
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/audit");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Audit Trail</title>", html);
        Assert.Contains("<h1>Audit Trail</h1>", html);
        Assert.Contains("class=\"active\" href=\"/audit\"", html);
        Assert.Contains("Live Monitor", html);
        Assert.DoesNotContain("href=\"/resources\"", html);
        Assert.Contains("Audit Insights", html);
        Assert.Contains("Total decisions", html);
        Assert.Contains("Most active identity", html);
        Assert.Contains("Most evaluated capability", html);
        Assert.Contains("Most matched policy", html);
        Assert.Contains("Average evaluation duration", html);
        Assert.True(
            html.IndexOf("Audit Insights", StringComparison.Ordinal) <
            html.IndexOf("Filter Audit Events", StringComparison.Ordinal));
        Assert.Contains("Audit Timeline", html);
        Assert.Contains("timeline-item", html);
        Assert.Contains("Recent Audit Events", html);
        Assert.Contains("audit-table", html);
        Assert.Contains("/audit/", html);
        Assert.Contains("View Decision Trace", html);
        Assert.Contains("Developer", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("decision-badge decision-allow", html);
        Assert.Contains("decision-badge decision-deny", html);
        Assert.Contains("decision-badge decision-pending", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("Developer is allowed to deploy applications to dev", html);
    }

    [Fact]
    public async Task Audit_TimelineRespectsFiltersAndLinksToTrace()
    {
        var matchingIdentity = $"timeline-agent-{Guid.NewGuid():N}";
        var otherIdentity = $"timeline-other-{Guid.NewGuid():N}";

        await PostEvaluationAsync(
            matchingIdentity,
            "DeployApplication",
            "dev");
        await PostEvaluationAsync(
            otherIdentity,
            "DeployApplication",
            "dev");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/audit?identityId={matchingIdentity}");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Audit Timeline", html);
        Assert.Contains("timeline-item", html);
        Assert.Contains(matchingIdentity, html);
        Assert.DoesNotContain(otherIdentity, html);
        Assert.Contains("Environment", html);
        Assert.Contains("View Decision Trace", html);
        Assert.Contains($"/audit/", html);
    }

    [Fact]
    public async Task Audit_InsightsRespectFilters()
    {
        var matchingIdentity = $"insight-agent-{Guid.NewGuid():N}";
        var otherIdentity = $"insight-other-{Guid.NewGuid():N}";

        await PostEvaluationAsync(
            matchingIdentity,
            "DeployApplication",
            "dev");
        await PostEvaluationAsync(
            matchingIdentity,
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            otherIdentity,
            "DeployApplication",
            "dev");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/audit?identityId={matchingIdentity}");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Audit Insights", html);
        AssertInsight(html, "Total decisions", "2");
        AssertInsight(html, "Most active identity", matchingIdentity);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("DeleteProductionDatabase", html);
        Assert.DoesNotContain(otherIdentity, html);
    }

    [Fact]
    public async Task Audit_InsightsRenderEmptyStates()
    {
        var unknownIdentity = $"missing-agent-{Guid.NewGuid():N}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/audit?identityId={unknownIdentity}");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Audit Insights", html);
        AssertInsight(html, "Total decisions", "0");
        AssertInsight(html, "Most active identity", "none");
        AssertInsight(html, "Most evaluated capability", "none");
        AssertInsight(html, "Most matched policy", "none");
        AssertInsight(html, "Average evaluation duration", "0 ms");
        Assert.Contains("No audit events yet", html);
        Assert.Contains(
            "Audit events are created automatically when decisions are evaluated.",
            html);
        Assert.Contains(
            "Users do not manually create audit events.",
            html);
        Assert.Contains(
            "seneschal evaluate payment-agent azure.keyvault.secret.read production",
            html);
    }

    [Fact]
    public async Task AuditDetail_RendersDecisionTrace()
    {
        await PostEvaluationAsync(
            "Developer",
            "DeployApplication",
            "dev");

        var auditEventId = await GetMostRecentAuditEventIdAsync();

        using var response = await _client.GetAsync($"/audit/{auditEventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Audit Event Detail", html);
        Assert.Contains("Decision Trace", html);
        Assert.Contains("Request", html);
        Assert.Contains("Policy Match", html);
        Assert.Contains("Decision", html);
        Assert.Contains("Obligations", html);
        Assert.Contains("Reason", html);
        Assert.Contains("TimestampUtc", html);
        Assert.Contains("IdentityId", html);
        Assert.Contains("Developer", html);
        Assert.Contains("CapabilityId", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("ResourceId", html);
        Assert.Contains("Environment", html);
        Assert.Contains("EnforcementMode", html);
        Assert.Contains("EvaluationDurationMs", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("Request Context", html);
        Assert.Contains("Policy Evaluation", html);
        Assert.Contains("Decision Resolution", html);
        Assert.Contains("Final Outcome", html);
        Assert.Contains("Winning policy", html);
        Assert.Contains("Evaluation latency", html);
        Assert.Contains("Condition evaluation", html);
        Assert.Contains("identity.id == Developer", html);
        Assert.Contains("capability.id == DeployApplication", html);
        Assert.Contains("resource.environment == dev", html);
        Assert.Contains("Expected", html);
        Assert.Contains("Actual", html);
        Assert.Contains("Matched Policies", html);
        Assert.Contains("identity.id mismatch", html);
    }

    [Fact]
    public async Task AuditDetail_UnknownIdReturnsFriendlyNotFound()
    {
        using var response = await _client.GetAsync("/audit/unknown-event");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Audit event not found", html);
        Assert.Contains("unknown-event", html);
        Assert.Contains("Back to Audit Trail", html);
    }

    [Fact]
    public async Task Audit_DefaultRequestStillReturnsJson()
    {
        await PostEvaluationAsync(
            "Developer",
            "DeployApplication",
            "dev");

        using var response = await _client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.True(document.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Audit_FiltersJsonByIdentityId()
    {
        var matchingIdentity = $"payment-agent-{Guid.NewGuid():N}";
        var otherIdentity = $"other-agent-{Guid.NewGuid():N}";

        await PostEvaluationAsync(
            matchingIdentity,
            "DeployApplication",
            "dev");
        await PostEvaluationAsync(
            otherIdentity,
            "DeployApplication",
            "dev");

        using var response = await _client.GetAsync(
            $"/audit?identityId={matchingIdentity}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        Assert.NotEmpty(document.RootElement.EnumerateArray());
        Assert.All(
            document.RootElement.EnumerateArray(),
            item => Assert.Equal(
                matchingIdentity,
                item.GetProperty("identityId").GetString()));
    }

    [Fact]
    public async Task Audit_FiltersJsonByDecisionCaseInsensitively()
    {
        await PostEvaluationAsync(
            "Developer",
            "DeployApplication",
            "dev");
        await PostEvaluationAsync(
            "Developer",
            "DeleteProductionDatabase",
            "prod");

        using var response = await _client.GetAsync("/audit?decision=Allow");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        Assert.NotEmpty(document.RootElement.EnumerateArray());
        Assert.All(
            document.RootElement.EnumerateArray(),
            item => Assert.Equal(
                "allow",
                item.GetProperty("decision").GetString()));
    }

    [Fact]
    public async Task Audit_FiltersJsonByCombinedFilters()
    {
        await PostEvaluationAsync(
            "Developer",
            "DeployApplication",
            "dev");
        await PostEvaluationAsync(
            "Developer",
            "DeployApplication",
            "prod");
        await PostEvaluationAsync(
            "SupportAgent",
            "DeployApplication",
            "dev");

        using var response = await _client.GetAsync(
            "/audit?identityId=Developer&environment=dev&matchedPolicy=Developers can deploy to dev");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        Assert.All(
            document.RootElement.EnumerateArray(),
            item =>
            {
                Assert.Equal(
                    "Developer",
                    item.GetProperty("identityId").GetString());
                Assert.Equal(
                    "dev",
                    item.GetProperty("environment").GetString());
                Assert.Contains(
                    item
                        .GetProperty("matchedPolicies")
                        .EnumerateArray(),
                    policy => policy.GetString() ==
                        "Developers can deploy to dev");
            });
    }

    [Fact]
    public async Task Audit_HtmlFilterFormPreservesSelectedValues()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/audit?identityId=payment-agent&capabilityId=DeployApplication&decision=Allow&enforcementMode=LogOnly&environment=dev&matchedPolicy=Developers%20can%20deploy%20to%20dev");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Filter Audit Events", html);
        Assert.Contains("<details class=\"panel filter-panel\" open>", html);
        Assert.Contains(
            "Narrow recent decisions by identity, capability, environment, policy, decision, or mode.",
            html);
        Assert.Contains(
            "name=\"identityId\" placeholder=\"payment-agent\" value=\"payment-agent\"",
            html);
        Assert.Contains(
            "name=\"capabilityId\" placeholder=\"azure.keyvault.secret.read\" value=\"DeployApplication\"",
            html);
        Assert.Contains("<select name=\"decision\">", html);
        Assert.Contains("<option value=\"allow\" selected>Allow</option>", html);
        Assert.Contains("<option value=\"deny\">Deny</option>", html);
        Assert.Contains(
            "<option value=\"requires_approval\">Pending Approval</option>",
            html);
        Assert.Contains("<select name=\"enforcementMode\">", html);
        Assert.Contains(
            "<option value=\"LogOnly\" selected>LogOnly</option>",
            html);
        Assert.Contains("Runtime mode", html);
        Assert.Contains("<option value=\"Enforce\">Enforce</option>", html);
        Assert.Contains(
            "name=\"environment\" placeholder=\"production\" value=\"dev\"",
            html);
        Assert.Contains(
            "name=\"matchedPolicy\" placeholder=\"prod-secret-read\" value=\"Developers can deploy to dev\"",
            html);
    }

    private async Task PostEvaluationAsync(
        string identity,
        string capability,
        string environment)
    {
        using var evaluationResponse = await _client.PostAsJsonAsync(
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

        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
    }

    private async Task<string> GetMostRecentAuditEventIdAsync()
    {
        using var response = await _client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());

        return document.RootElement[0]
            .GetProperty("id")
            .GetString()!;
    }

    private static void AssertInsight(
        string html,
        string label,
        string value)
    {
        Assert.Contains(
            $"<strong>{WebUtility.HtmlEncode(value)}</strong>",
            html);
        Assert.Contains(
            $"<span>{WebUtility.HtmlEncode(label)}</span>",
            html);
    }
}
