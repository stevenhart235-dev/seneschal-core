using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GovernanceModePageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public GovernanceModePageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GovernancePage_RendersActiveLogOnlyStateAndOnlyEnforceAction()
    {
        using var client = CreateIsolatedClient();

        using var response = await client.GetAsync("/governance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Runtime Governance", html);
        Assert.Contains("MONITORING ACTIVE", html);
        Assert.Contains("Pending Approval decisions are recorded", html);
        Assert.Contains("Canonical mode: <strong>LogOnly</strong>", html);
        Assert.Contains("Current", html);
        Assert.Contains("Switch to Enforce", html);
        Assert.DoesNotContain("Return to LogOnly?", html);
        Assert.Contains("Enable runtime enforcement?", html);
        Assert.Contains("Existing Allow decisions will continue", html);
        Assert.Contains("Runtime governance mode is in memory", html);
        Assert.Contains("resets to LogOnly when Seneschal restarts", html);
    }

    [Fact]
    public async Task GovernancePage_RendersActiveEnforceStateAndOnlyLogOnlyAction()
    {
        using var client = CreateIsolatedClient();
        await SetModeAsync(client, "Enforce");

        var html = await client.GetStringAsync("/governance");

        Assert.Contains("ENFORCEMENT ACTIVE", html);
        Assert.Contains("Pending Approval decisions are projected as blocked", html);
        Assert.Contains("Canonical mode: <strong>Enforce</strong>", html);
        Assert.Contains("Return to LogOnly", html);
        Assert.Contains("Return to LogOnly?", html);
        Assert.Contains("will no longer block integrated operations", html);
        Assert.DoesNotContain("Enable runtime enforcement?", html);
        Assert.DoesNotContain(">Switch to Enforce</button>", html);
    }

    [Fact]
    public async Task EnforceConfirmation_RendersRecentImpact()
    {
        using var client = CreateIsolatedClient();
        using (var denied = await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "governance-impact-denied",
            capability = "database.migration.unmatched",
            context = new { environment = "production", resource = "db" }
        }))
        {
            Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        }
        using (var pending = await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "release-approval-worker",
            capability = "production.release.approve",
            context = new { environment = "production", resource = "checkout-api" }
        }))
        {
            Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        }

        var html = await client.GetStringAsync("/governance");

        Assert.Contains("2 active identities", html);
        Assert.Contains("2 active capabilities", html);
        Assert.Contains("1 recent denied decisions", html);
        Assert.Contains("1 recent pending approvals", html);
        Assert.Contains("database.migration.unmatched", html);
        Assert.Contains("would currently be blocked.", html);
        Assert.Contains("production.release.approve", html);
        Assert.Contains("would currently be blocked pending approval.", html);
    }

    [Fact]
    public async Task SwitchingToEnforce_AffectsSubsequentEvaluate()
    {
        using var client = CreateIsolatedClient();

        await SetModeAsync(client, "Enforce");

        using var response = await PostEvaluationAsync(
            client,
            $"GovernanceEnforce-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("deny", root.GetProperty("decision").GetString());
        Assert.Equal("deny", root.GetProperty("effectiveAction").GetString());
        Assert.Equal("Enforce", root.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task SwitchingBackToLogOnly_AffectsSubsequentEvaluate()
    {
        using var client = CreateIsolatedClient();

        await SetModeAsync(client, "Enforce");
        await SetModeAsync(client, "LogOnly");

        using var response = await PostEvaluationAsync(
            client,
            $"GovernanceLogOnly-{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("deny", root.GetProperty("decision").GetString());
        Assert.Equal(
            "logged_only",
            root.GetProperty("effectiveAction").GetString());
        Assert.Equal("LogOnly", root.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task Diagnostics_ReflectsCurrentModeAfterSwitch()
    {
        using var client = CreateIsolatedClient();

        await SetModeAsync(client, "Enforce");

        using var response = await client.GetAsync("/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);

        Assert.Equal(
            "Enforce",
            document.RootElement.GetProperty("currentRuntimeMode").GetString());
    }

    [Fact]
    public async Task AuditEvents_PreserveModeUsedAtEvaluationTime()
    {
        using var client = CreateIsolatedClient();
        var enforceIdentity = $"GovernanceAuditEnforce-{Guid.NewGuid():N}";
        var logOnlyIdentity = $"GovernanceAuditLogOnly-{Guid.NewGuid():N}";

        await SetModeAsync(client, "Enforce");
        using (var enforceResponse = await PostEvaluationAsync(
            client,
            enforceIdentity))
        {
            Assert.Equal(HttpStatusCode.OK, enforceResponse.StatusCode);
        }

        await SetModeAsync(client, "LogOnly");
        using (var logOnlyResponse = await PostEvaluationAsync(
            client,
            logOnlyIdentity))
        {
            Assert.Equal(HttpStatusCode.OK, logOnlyResponse.StatusCode);
        }

        using var auditResponse = await client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        using var document = await ReadJsonAsync(auditResponse);
        var events = document.RootElement.EnumerateArray().ToList();

        var enforceEvent = events.Single(item =>
            item.GetProperty("identityId").GetString() == enforceIdentity);
        var logOnlyEvent = events.Single(item =>
            item.GetProperty("identityId").GetString() == logOnlyIdentity);

        Assert.Equal(
            "Enforce",
            enforceEvent.GetProperty("enforcementMode").GetString());
        Assert.Equal(
            "LogOnly",
            logOnlyEvent.GetProperty("enforcementMode").GetString());
    }

    private HttpClient CreateIsolatedClient()
    {
        return _factory
            .WithWebHostBuilder(_ => { })
            .CreateClient();
    }

    private static async Task SetModeAsync(HttpClient client, string mode)
    {
        using var response = await client.PostAsync(
            "/governance?handler=SetMode",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("mode", mode)
            ]));

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect,
            $"Expected governance mode switch to succeed, got {response.StatusCode}.");
    }

    private static async Task<HttpResponseMessage> PostEvaluationAsync(
        HttpClient client,
        string identity)
    {
        return await client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability = "DeployApplication",
                context = new
                {
                    environment = "dev",
                    resource = "governance-mode-test-resource"
                }
            });
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
