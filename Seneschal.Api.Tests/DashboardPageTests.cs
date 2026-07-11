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
        Assert.Contains("Runtime governance across integrated applications.", html);
        Assert.Contains("Overview", html);
        Assert.Contains("Governance", html);
        Assert.Contains("Operations", html);
        Assert.Contains("Explore", html);
        Assert.Contains("<span>Dashboard</span>", html);
        Assert.Contains("class=\"active\" href=\"/dashboard\"", html);
        Assert.Contains("Monitor", html);
        Assert.Contains("/monitor", html);
        Assert.Contains("/resources", html);
        Assert.Contains("/graph-view", html);
        Assert.Contains("Runtime posture", html);
        Assert.Contains("Live Decision Activity", html);
        Assert.Contains("Needs Attention", html);
        Assert.Contains("Most Active Capabilities", html);
        Assert.Contains("Governance coverage", html);
        Assert.Contains("Capability Activity", html);
        Assert.Contains("/capability-activity", html);
        Assert.Contains("Identity Activity", html);
        Assert.Contains("/identity-activity", html);
        Assert.Contains("Total decisions", html);
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

        Assert.Contains("No evaluations have been recorded", html);
        Assert.Contains("Runtime posture", html);
        Assert.Contains("Governance coverage", html);
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

        Assert.Contains("Total decisions", html);
        Assert.Contains("Decision Distribution", html);
        Assert.Contains("Live Decision Activity", html);
        Assert.Contains("Most Active Capabilities", html);
        Assert.Contains(capability, html);
        Assert.Contains(identity, html);
        Assert.Contains("Governance coverage", html);
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
        Assert.Contains("Runtime posture", html);
        Assert.Contains("Live Decision Activity", html);
        Assert.Contains("Most Active Capabilities", html);
    }

    [Fact]
    public async Task Resources_RendersFromNavigationRoute()
    {
        using var response = await _client.GetAsync("/resources");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Governance / Resources", html);
        Assert.Contains("Resource Explorer Coming Soon", html);
        Assert.Contains("class=\"active\" href=\"/resources\"", html);
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

        Assert.Contains("Runtime posture", html);
        Assert.Contains("Canonical mode:", html);
        Assert.Contains("LogOnly", html);
        Assert.Contains("Live Decision Activity", html);
        Assert.Contains(identity, html);
        Assert.Contains("Executed and recorded", html);
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

        Assert.Equal("Executed and recorded", snapshot.Decisions.Single(item => item.Id == "log-only").EffectiveAction);
        Assert.Equal("Blocked", snapshot.Decisions.Single(item => item.Id == "enforce").EffectiveAction);
        Assert.Equal("Executed", snapshot.Decisions.Single(item => item.Id == "allow").EffectiveAction);
        Assert.Equal("Executed and recorded", snapshot.Decisions.Single(item => item.Id == "pending-log").EffectiveAction);
        Assert.Equal("Blocked pending approval", snapshot.Decisions.Single(item => item.Id == "pending-enforce").EffectiveAction);
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
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("release-approval-worker", html);
        Assert.Contains("Pending Approval", html);
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
