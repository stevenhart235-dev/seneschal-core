using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class MonitorPageTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public MonitorPageTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task EmptyMonitorRendersOperationalStatusAndFallback()
    {
        using var client = CreateClient();
        var html = await GetMonitor(client);

        Assert.Contains("<title>Live Monitor</title>", html);
        Assert.Contains("<h1>Live Monitor</h1>", html);
        Assert.Contains("Watch current evaluations, runtime health, and events requiring attention.", html);
        Assert.Contains("Operational status", html);
        Assert.Contains("Healthy", html);
        Assert.Contains("Canonical mode: <b>LogOnly</b>", html);
        Assert.Contains("None active", html);
        Assert.Contains("No evaluations observed", html);
        Assert.Contains("No runtime evaluations observed", html);
        Assert.Contains("No requests waiting", html);
        Assert.Contains("No incidents recorded", html);
        Assert.Contains("Live refresh requires JavaScript", html);
    }

    [Theory]
    [InlineData(EnforcementMode.LogOnly, "Monitoring active")]
    [InlineData(EnforcementMode.Enforce, "Enforcement active")]
    public async Task MonitorRendersCanonicalRuntimeMode(
        EnforcementMode mode, string posture)
    {
        using var client = CreateClient(mode: mode);
        var html = await GetMonitor(client);

        Assert.Contains(posture, html);
        Assert.Contains($"Canonical mode: <b>{mode}</b>", html);
        Assert.Contains(mode == EnforcementMode.Enforce
            ? "denied and pending decisions may block callers"
            : "recorded without blocking", html);
    }

    [Fact]
    public async Task ActiveGovernanceWindowAppearsInPostureAndAttention()
    {
        var window = new InMemoryGovernanceWindowStore();
        window.SetState(true, GovernanceWindowMode.Enforce);
        using var client = CreateClient(window: window);
        var html = await GetMonitor(client);

        Assert.Contains("Production Freeze", html);
        Assert.Contains("Enforce", html);
        Assert.Contains("Active governance windows", html);
        Assert.Contains("href=\"/governance-windows\"", html);
    }

    [Fact]
    public async Task LiveStreamRendersDecisionContextSafely()
    {
        var auditEvent = Event(
            id: "trace-1",
            identity: "worker&lt;unsafe>",
            capability: "production.release.approve",
            decision: DecisionType.RequireApproval,
            mode: EnforcementMode.Enforce) with
        {
            GovernanceWindowName = "Freeze <window>",
            GovernanceWindowMode = "Enforce",
            ApprovalOperationId = "release-<001>"
        };
        using var client = CreateClient(events: [auditEvent]);
        var html = await GetMonitor(client);

        Assert.Contains("Live decision stream", html);
        Assert.Contains("Pending Approval", html);
        Assert.Contains("Caller should pause and retry", html);
        Assert.Contains("Window: Freeze &lt;window&gt; (Enforce)", html);
        Assert.Contains("release-&lt;001&gt;", html);
        Assert.Contains("href=\"/audit/trace-1\"", html);
        Assert.Contains("View Decision Trace", html);
        Assert.DoesNotContain("Executed", html);
        Assert.Contains("worker&amp;lt;unsafe&gt;", html);
        Assert.DoesNotContain("Freeze <window>", html);
    }

    [Fact]
    public async Task AttentionLinksToPendingApprovalsAndRuntimeDrilldowns()
    {
        var approvals = new InMemoryApprovalStore();
        approvals.GetOrCreate("worker", "capability", "production", "resource",
            "Needs approval", DateTimeOffset.UtcNow, "operation-1");
        var denied = Event("deny-trace", "denied-worker", "dangerous.capability",
            DecisionType.Deny, EnforcementMode.LogOnly);
        using var client = CreateClient(events: [denied], approvals: approvals);
        var html = await GetMonitor(client);

        Assert.Contains("Pending approvals", html);
        Assert.Contains("Awaiting human resolution", html);
        Assert.Contains("href=\"/approvals\"", html);
        Assert.Contains("Most denied capability", html);
        Assert.Contains("dangerous.capability", html);
        Assert.Contains("Most active identity", html);
        Assert.Contains("denied-worker", html);
    }

    [Fact]
    public async Task ExistingPollingHooksAndLocalAssetRender()
    {
        using var client = CreateClient();
        var html = await GetMonitor(client);

        Assert.Contains("id=\"monitor-console\"", html);
        Assert.Contains("id=\"monitor-polling-status\"", html);
        Assert.Contains("id=\"monitor-health-polling\"", html);
        Assert.Contains("src=\"/monitor-live.js\"", html);
        var script = await client.GetStringAsync("/monitor-live.js");
        Assert.Contains("const intervalMs = 3000", script);
        Assert.Contains("document.hidden", script);
        Assert.Contains("cache: \"no-store\"", script);
        Assert.Contains("Stale · refresh unavailable", script);
    }

    [Fact]
    public async Task DashboardStillLinksToMonitor()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/dashboard");
        Assert.Contains("href=\"/monitor\"", html);
    }

    private HttpClient CreateClient(
        EnforcementMode mode = EnforcementMode.LogOnly,
        IReadOnlyCollection<AuditEvent>? events = null,
        InMemoryGovernanceWindowStore? window = null,
        InMemoryApprovalStore? approvals = null)
    {
        var audit = new InMemoryAuditEventStore();
        var activity = new InMemoryActivityStore();
        foreach (var item in events ?? [])
        {
            audit.WriteAsync(item).GetAwaiter().GetResult();
            activity.RecordAsync(item).GetAwaiter().GetResult();
        }

        return _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuditEventStore>();
            services.RemoveAll<IAuditSink>();
            services.RemoveAll<IActivityStore>();
            services.RemoveAll<IGovernanceModeStore>();
            services.RemoveAll<IGovernanceWindowStore>();
            services.RemoveAll<IApprovalStore>();
            services.AddSingleton<IAuditEventStore>(audit);
            services.AddSingleton<IAuditSink>(audit);
            services.AddSingleton<IActivityStore>(activity);
            services.AddSingleton<IGovernanceModeStore>(new FixedModeStore(mode));
            services.AddSingleton<IGovernanceWindowStore>(window ?? new InMemoryGovernanceWindowStore());
            services.AddSingleton<IApprovalStore>(approvals ?? new InMemoryApprovalStore());
        })).CreateClient();
    }

    private static async Task<string> GetMonitor(HttpClient client)
    {
        using var response = await client.GetAsync("/monitor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static AuditEvent Event(
        string id, string identity, string capability,
        DecisionType decision, EnforcementMode mode) => new()
    {
        Id = id,
        RequestId = $"request-{id}",
        TimestampUtc = DateTimeOffset.UtcNow,
        IdentityId = identity,
        CapabilityId = capability,
        Environment = "production",
        ResourceId = "checkout-api",
        Decision = decision,
        PolicyDecision = decision,
        EnforcementMode = mode,
        Reason = "Recorded reason",
        EvaluationDurationMs = 4,
        MatchedPolicies = ["policy-1"]
    };

    private sealed class FixedModeStore(EnforcementMode mode) : IGovernanceModeStore
    {
        private EnforcementMode _mode = mode;
        public EnforcementMode GetMode() => _mode;
        public void SetMode(EnforcementMode value) => _mode = value;
    }
}
