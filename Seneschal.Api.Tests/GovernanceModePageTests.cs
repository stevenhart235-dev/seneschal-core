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
    public async Task GovernancePage_RendersCurrentMode()
    {
        using var client = CreateIsolatedClient();

        using var response = await client.GetAsync("/governance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Runtime Governance", html);
        Assert.Contains("Current Mode: LogOnly", html);
        Assert.Contains("Switch to Enforce", html);
        Assert.Contains("Switch to LogOnly", html);
        Assert.Contains("Deny and pending approval decisions may block", html);
        Assert.Contains("integrated applications", html);
        Assert.Contains("Restarting", html);
        Assert.Contains("resets the mode to LogOnly", html);
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
