using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Api.Pages;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DashboardPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public DashboardPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_RendersRuntimeCommandCenterLayout()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<h1>Dashboard</h1>", html);
        Assert.Matches(
            "href=\"/styles\\.css\\?v=[^\"]+\"",
            html);
        Assert.Equal(1, html.Split("aria-label=\"Dashboard live status\"").Length - 1);
        Assert.Contains("id=\"dashboard-live-status-label\">Connecting</strong>", html);
        Assert.DoesNotContain(">Unavailable</strong>", html);
        Assert.Contains("id=\"governance-posture-mode\"", html);
        Assert.Contains("id=\"dashboard-last-updated\"", html);
        Assert.Contains("id=\"dashboard-refresh\" aria-label=\"Refresh Dashboard status\"", html);
        Assert.Contains("Overview", html);
        Assert.Contains("Governance", html);
        Assert.Contains("Operations", html);
        Assert.Contains("Explore", html);
        Assert.Contains("<span>Dashboard</span>", html);
        Assert.Contains("class=\"active\" href=\"/dashboard\"", html);
        Assert.Contains("Live Monitor", html);
        Assert.Contains("/monitor", html);
        Assert.DoesNotContain("href=\"/resources\"", html);
        Assert.Contains("/graph-view", html);
        Assert.Contains("demo-metric-strip", html);
        Assert.Contains("Active capabilities", html);
        Assert.Contains("Denied decisions", html);
        Assert.Contains("Pending approvals", html);
        Assert.Contains("Active windows", html);
        Assert.Contains("Recent evaluations", html);
        Assert.Contains("Technology Posture", html);
        Assert.Contains("dashboard-technology-grid", html);
        Assert.Contains("dashboard-technology-card", html);
        Assert.Contains("dashboard-technology-card__icon", html);
        Assert.Contains("dashboard-technology-card__header", html);
        Assert.Contains("dashboard-technology-card__identity", html);
        Assert.Contains("dashboard-technology-card__metrics", html);
        var technologyCardCount = html.Split(
            "class=\"dashboard-technology-card ",
            StringSplitOptions.None).Length - 1;
        Assert.InRange(technologyCardCount, 1, 5);
        Assert.Equal(
            technologyCardCount,
            html.Split(
                "class=\"dashboard-technology-card__metrics\"",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("class=\"technology-card", html);
        Assert.DoesNotContain("class=\"technology-grid", html);
        Assert.Contains("href=\"/technologies/azure\"", html);
        Assert.Contains("src=\"/technology-icons/azure.svg\"", html);
        Assert.Contains("width=\"32\" height=\"32\"", html);
        Assert.Contains("<dt>Denied</dt>", html);
        Assert.Contains("<dt>Pending</dt>", html);
        Assert.Contains("<dt>Evaluations</dt>", html);
        Assert.Contains("Explore all technologies", html);
        Assert.DoesNotContain(
            "Azure capabilities and their runtime governance evidence.",
            html);
        Assert.DoesNotContain("Operational Feed", html);
        Assert.DoesNotContain("Investigation Queue", html);
        Assert.Contains("Top Capabilities", html);
        Assert.DoesNotContain("id=\"live-decision-feed\"", html);
        Assert.DoesNotContain("id=\"dashboard-investigation-queue\"", html);
        Assert.Contains("Runtime Summary", html);
        Assert.DoesNotContain("Operational posture", html);
        Assert.DoesNotContain("dashboard-posture-panel", html);
        Assert.DoesNotContain("attention-summary", html);
        Assert.DoesNotContain("Governance coverage", html);
        Assert.DoesNotContain("Live runtime governance", html);
        Assert.DoesNotContain("Newest decisions first", html);
        Assert.DoesNotContain("Overall operational status", html);
        Assert.DoesNotContain("Canonical mode", html);
        Assert.DoesNotContain("Seneschal is responding", html);
        Assert.DoesNotContain("Supporting metrics", html);
        Assert.DoesNotContain("What needs attention", html);
        Assert.Contains("Capability Activity", html);
        Assert.Contains("/capability-activity", html);
        Assert.Contains("Identity Activity", html);
        Assert.Contains("/identity-activity", html);
        Assert.Contains("Recent evaluations", html);
        Assert.Contains("Capabilities", html);
        Assert.Contains("Policies", html);
        Assert.Contains("Identities", html);
        Assert.Contains("/capability-explorer", html);
        Assert.DoesNotContain("First-Run Guide", html);
        Assert.DoesNotContain("Quick Actions", html);
        Assert.DoesNotContain("Highest Risk Capabilities", html);
        Assert.DoesNotContain("Recently Added Capabilities", html);
    }

    [Fact]
    public async Task Dashboard_RendersFriendlyEmptyStateWhenNoActivityExists()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.RemoveAll<IAuditEventStore>();
                    services.RemoveAll<IAuditSink>();
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                    services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
                    services.AddSingleton<IAuditSink>(
                        services => services.GetRequiredService<IAuditEventStore>());
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Technology Posture", html);
        Assert.Contains("not observed", html);
        Assert.Contains("Explore all technologies", html);
        Assert.DoesNotContain("Operational Feed", html);
        Assert.DoesNotContain("Investigation Queue", html);
        Assert.DoesNotContain("Operational posture", html);
        Assert.DoesNotContain("Governance coverage", html);
        Assert.DoesNotContain("First-Run Guide", html);
    }

    [Fact]
    public async Task Dashboard_RendersActivityAfterEvaluation()
    {
        var identity = $"DashboardActivity-{Guid.NewGuid():N}";
        var capability = $"DashboardCapability-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "dashboard-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Recent evaluations", html);
        Assert.Contains("Decision Distribution", html);
        Assert.Contains("Technology Posture", html);
        Assert.Contains("Top Capabilities", html);
        Assert.Contains(capability, html);
        Assert.DoesNotContain("Operational Feed", html);
    }

    [Fact]
    public async Task Dashboard_KeepsOperationalStoryAfterRuntimeActivityGrows()
    {
        var identity = $"DashboardFirstRun-{Guid.NewGuid():N}";

        for (var index = 0; index < 3; index++)
        {
            using var evaluationResponse = await _client.PostAsJsonAsync(
                "/evaluate",
                new
                {
                    identity,
                    capability = $"DashboardFirstRunCapability-{index}",
                    context = new
                    {
                        environment = "dev",
                        resource = $"dashboard-first-run-resource-{index}"
                    }
                });

            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("First-Run Guide", html);
        Assert.Contains("Technology Posture", html);
        Assert.DoesNotContain("Operational Feed", html);
        Assert.DoesNotContain("Investigation Queue", html);
        Assert.Contains("Top Capabilities", html);
    }

    [Fact]
    public async Task Resources_RendersFromNavigationRoute()
    {
        using var response = await _client.GetAsync("/resources");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Governance / Resources", html);
        Assert.Contains("Resource Explorer Coming Soon", html);
        Assert.DoesNotContain("href=\"/resources\"", html);
    }

    [Fact]
    public async Task Dashboard_RendersCurrentModeAndLatestEvaluations()
    {
        var identity = $"DashboardLive-{Guid.NewGuid():N}";
        using var evaluation = await _client.PostAsJsonAsync("/evaluate", new
        {
            identity,
            capability = "dashboard.live.test",
            context = new { environment = "dev", resource = "test" }
        });
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);

        var html = await _client.GetStringAsync("/dashboard");

        Assert.Contains("Runtime Governance", html);
        Assert.Contains("LogOnly", html);
        Assert.Contains("Technology Posture", html);
        Assert.DoesNotContain("Operational Feed", html);
        Assert.DoesNotContain(identity, html);
    }

    [Fact]
    public void LiveSnapshot_DerivesLogOnlyAndEnforceActions()
    {
        var now = DateTimeOffset.UtcNow;
        var events = new[]
        {
            CreateAuditEvent("log-only", now, "one", DecisionType.Deny, EnforcementMode.LogOnly),
            CreateAuditEvent("enforce", now.AddSeconds(-1), "two", DecisionType.Deny, EnforcementMode.Enforce),
            CreateAuditEvent("allow", now.AddSeconds(-2), "three", DecisionType.Allow, EnforcementMode.Enforce),
            CreateAuditEvent("pending-log", now.AddSeconds(-3), "four", DecisionType.RequireApproval, EnforcementMode.LogOnly),
            CreateAuditEvent("pending-enforce", now.AddSeconds(-4), "five", DecisionType.RequireApproval, EnforcementMode.Enforce)
        };

        var snapshot = DashboardModel.CreateLiveSnapshot(events, EnforcementMode.Enforce, now);

        Assert.Equal("Recorded; caller may continue", snapshot.Decisions.Single(item => item.Id == "log-only").EffectiveAction);
        Assert.Equal("Caller should block the operation", snapshot.Decisions.Single(item => item.Id == "enforce").EffectiveAction);
        Assert.Equal("Caller may proceed", snapshot.Decisions.Single(item => item.Id == "allow").EffectiveAction);
        Assert.Equal("Recorded; caller may continue", snapshot.Decisions.Single(item => item.Id == "pending-log").EffectiveAction);
        Assert.Equal("Caller should pause and retry", snapshot.Decisions.Single(item => item.Id == "pending-enforce").EffectiveAction);
    }

    [Theory]
    [InlineData("Deny", "LogOnly", "Continue (recorded)")]
    [InlineData("PendingApproval", "LogOnly", "Continue (recorded)")]
    [InlineData("Deny", "Enforce", "Block")]
    [InlineData("PendingApproval", "Enforce", "Wait for approval")]
    public void DashboardActionLabel_IsDecisionAndModeAware(
        string decision, string mode, string expected)
    {
        Assert.Equal(expected, DashboardModel.DisplayEffectiveAction(decision, mode));
    }

    [Fact]
    public void LiveSnapshot_OrdersIdentitiesAndAppliesIdleThreshold()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = DashboardModel.CreateLiveSnapshot(
            new[]
            {
                CreateAuditEvent("old", now.AddSeconds(-21), "idle-worker", DecisionType.Allow, EnforcementMode.LogOnly),
                CreateAuditEvent("new", now.AddSeconds(-2), "live-worker", DecisionType.Allow, EnforcementMode.LogOnly)
            },
            EnforcementMode.LogOnly,
            now);

        Assert.Equal(new[] { "live-worker", "idle-worker" }, snapshot.Identities.Select(item => item.Identity));
        Assert.Equal("Live", snapshot.Identities.First().Status);
        Assert.Equal("Idle", snapshot.Identities.Last().Status);
        Assert.Equal(1, snapshot.ActiveIdentityCount);
    }

    [Fact]
    public async Task DashboardLiveEndpoint_ReturnsSafePresentationFields()
    {
        using var response = await _client.GetAsync("/dashboard?handler=Live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"currentMode\"", json);
        Assert.Contains("\"totalDecisions\"", json);
        Assert.Contains("\"activeIdentityCount\"", json);
        Assert.Contains("\"decisions\"", json);
        Assert.Contains("\"topCapabilities\"", json);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DashboardPolling_UsesStableTargetsAndOneRefreshPath()
    {
        var apiDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Seneschal.Api"));
        var script = File.ReadAllText(Path.Combine(
            apiDirectory, "wwwroot", "dashboard-live.js"));

        Assert.DoesNotContain("#live-decision-feed", script);
        Assert.DoesNotContain("#dashboard-investigation-queue", script);
        Assert.Contains("requiredElement(\"#top-capability-list\")", script);
        Assert.Contains("let requestInFlight = false", script);
        Assert.Contains("if (document.hidden || requestInFlight) return", script);
        Assert.Contains("finally", script);
        Assert.Contains("requestInFlight = false", script);
        Assert.Equal(1, script.Split("async function refresh()").Length - 1);
        Assert.Contains("addEventListener(\"click\", refresh)", script);
        Assert.Contains("setInterval(refresh, intervalMs)", script);
        Assert.Contains("refresh();", script);
        Assert.Contains("#dashboard-live-status-label\").textContent = \"Live\"", script);
        Assert.Contains("#dashboard-live-status-label\").textContent = \"Unavailable\"", script);
        Assert.DoesNotContain("demo-feed-event", script);
    }

    [Fact]
    public void DashboardTechnologyCards_UseBoundedDashboardOnlyIconSizing()
    {
        var apiDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Seneschal.Api"));
        var styles = File.ReadAllText(Path.Combine(
            apiDirectory, "wwwroot", "styles.css"));

        Assert.Contains(".dashboard-technology-grid", styles);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr))", styles);
        Assert.Contains(".dashboard-technology-card__icon", styles);
        Assert.Contains("height: 40px", styles);
        Assert.Contains("width: 40px", styles);
        Assert.Contains(".dashboard-technology-card__icon img", styles);
        Assert.Contains("max-height: 30px", styles);
        Assert.Contains("max-width: 30px", styles);
        Assert.Contains("min-height: 118px", styles);
        Assert.Contains(".dashboard-technology-card:link", styles);
        Assert.Contains("text-decoration: none", styles);
        Assert.Contains(".dashboard-status-group", styles);
        Assert.Contains("display: flex", styles);
        Assert.Contains("flex-wrap: wrap", styles);
        Assert.Contains("@media (max-width: 1150px)", styles);
        Assert.Contains("@media (max-width: 900px)", styles);
        Assert.Contains("@media (max-width: 680px)", styles);
    }

    [Fact]
    public async Task ApprovalWorkerPolicy_ReturnsPendingApprovalAndRendersLiveData()
    {
        using var evaluation = await _client.PostAsJsonAsync("/evaluate", new
        {
            identity = "release-approval-worker",
            capability = "production.release.approve",
            context = new { environment = "production", resource = "checkout-api" }
        });
        Assert.Equal(HttpStatusCode.OK, evaluation.StatusCode);
        var result = await evaluation.Content.ReadFromJsonAsync<Seneschal.Api.Models.DecisionResult>();
        Assert.NotNull(result);
        Assert.Equal("requires_approval", result.Decision);

        var html = await _client.GetStringAsync("/dashboard");
        Assert.Contains("Pending Approval", html);
        Assert.Contains("href=\"/approvals\"", html);
        Assert.DoesNotContain("demo-investigation-queue", html);
        Assert.Contains("Technology Posture", html);
        Assert.Contains("Decision Distribution", html);
        Assert.Contains("distribution-pending", html);

        var json = await _client.GetStringAsync("/dashboard?handler=Live");
        Assert.Contains("release-approval-worker", json);
        Assert.Contains("PendingApproval", json);
        Assert.DoesNotContain("dev-release-approval-worker-key", json);
    }

    [Fact]
    public void ApprovalWorkerIntegrationKey_HasExactDevelopmentScope()
    {
        var apiDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Seneschal.Api"));
        var loader = new IntegrationApiKeyLoader(Path.Combine(
            apiDirectory, "Policies", "integration-keys.yaml"));
        var key = Assert.Single(loader.GetKeys(), item =>
            item.Name == "release-approval-worker-lab");

        Assert.True(key.Enabled);
        Assert.Equal("production", key.Environment);
        Assert.Equal(["release-approval-worker"], key.AllowedIdentities);
        Assert.Equal(["production.release.approve"], key.AllowedCapabilities);
    }

    private static AuditEvent CreateAuditEvent(
        string id,
        DateTimeOffset timestamp,
        string identity,
        DecisionType decision,
        EnforcementMode mode) => new()
        {
            Id = id,
            TimestampUtc = timestamp,
            IdentityId = identity,
            CapabilityId = $"capability-{identity}",
            Decision = decision,
            EnforcementMode = mode,
            Reason = "Test decision"
        };
}
