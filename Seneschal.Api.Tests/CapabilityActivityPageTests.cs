using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityActivityPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public CapabilityActivityPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CapabilityActivity_RendersFriendlyEmptyState()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/capability-activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capability Activity", html);
        Assert.Contains("No Runtime Activity Yet", html);
        Assert.Contains("Capability activity appears automatically", html);
        Assert.Contains("/evaluate", html);
    }

    [Fact]
    public async Task CapabilityActivity_RendersCapabilityActivityAndDetail()
    {
        var identity = $"CapabilityActivityIdentity-{Guid.NewGuid():N}";
        var capability = $"CapabilityActivity-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "capability-activity-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync(
            $"/capability-activity?capabilityId={Uri.EscapeDataString(capability)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Evaluation attempt", html);
        Assert.Contains("Allow decision", html);
        Assert.Contains("Denied evaluation", html);
        Assert.Contains("Pending approval", html);
        Assert.Contains("Most recent activity", html);
        Assert.Contains(capability, html);
        Assert.Contains("Allow decision", html);
        Assert.Contains("Denied evaluation", html);
        Assert.Contains("Pending approval", html);
        Assert.Contains("Most recent activity", html);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains("All Capability Activity", html);
        Assert.Contains(
            $"/audit?capabilityId={Uri.EscapeDataString(capability)}",
            html);
    }

    [Fact]
    public async Task SelectedCatalogCapability_RendersDescriptiveMetadata()
    {
        using var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "Developer",
                capability = "DeployApplication",
                context = new { environment = "dev", resource = "metadata-test" }
            });
        Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);

        var html = await _client.GetStringAsync(
            "/capability-activity?capabilityId=DeployApplication");

        Assert.Contains("Deploy Application", html);
        Assert.Contains("Deploy an application to a managed environment.", html);
        Assert.Contains("Medium risk", html);
        Assert.Contains("Deployment", html);
        Assert.Contains("<code>DeployApplication</code>", html);
    }

    [Fact]
    public async Task Dashboard_LinksToCapabilityActivity()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capability Activity", html);
        Assert.Contains("/capability-activity", html);
    }

    [Fact]
    public async Task SelectedCapability_RendersChronologicalGroupedTimelineAndLifecycle()
    {
        var capability = $"release-&-timeline-{Guid.NewGuid():N}";
        var activity = new InMemoryActivityStore();
        var audit = new InMemoryAuditEventStore();
        var approvals = new InMemoryApprovalStore();
        var now = DateTimeOffset.UtcNow;
        var approval = approvals.GetOrCreate("release-worker", capability,
            "production", "checkout-api", "Approval required", now.AddMinutes(-5),
            "release-007").Record;
        approvals.Resolve(approval.Id, ApprovalStatus.Approved, "operator", now.AddMinutes(-4));
        approvals.Consume(approval.Id, "allow-final", now.AddMinutes(-2));

        var events = new[]
        {
            Event("pending-first", now.AddMinutes(-5), capability,
                DecisionType.RequireApproval, EnforcementMode.Enforce,
                operationId: "release-007", approvalId: approval.Id,
                approvalStatus: "Pending", approvalAction: "Requested",
                reason: "Approval is required."),
            Event("approved-middle", now.AddMinutes(-4), capability,
                DecisionType.Allow, EnforcementMode.Enforce,
                operationId: "release-007", approvalId: approval.Id,
                approvalStatus: "Approved", approvalAction: "Approved",
                reason: "Approval approved by operator."),
            Event("allow-final", now.AddMinutes(-2), capability,
                DecisionType.Allow, EnforcementMode.Enforce,
                operationId: "release-007", approvalId: approval.Id,
                approvalStatus: "Consumed", approvalAction: "Consumed",
                reason: "Approved operation retried."),
            Event("legacy-deny", now.AddMinutes(-1), capability,
                DecisionType.Deny, EnforcementMode.LogOnly,
                reason: "Denied by policy."),
            Event("rejected-lifecycle", now, capability,
                DecisionType.Deny, EnforcementMode.Enforce,
                operationId: "release-008", approvalStatus: "Rejected",
                approvalAction: "Rejected", reason: "Approval rejected.")
        };
        foreach (var item in events) { await audit.WriteAsync(item); await activity.RecordAsync(item); }

        using var client = CreateClient(activity, audit, approvals);
        using var response = await client.GetAsync(
            $"/capability-activity?capabilityId={Uri.EscapeDataString(capability)}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Investigation timeline", html);
        Assert.Contains("Application operation", html);
        Assert.Contains("release-007", html);
        Assert.Contains("Pending Approval", html);
        Assert.Contains("Approved", html);
        Assert.Contains("Consumed", html);
        Assert.Contains("Rejected", html);
        Assert.Contains("Legacy context evaluations", html);
        Assert.Contains("Recorded; caller may continue", html);
        Assert.Contains("View Decision Trace", html);
        Assert.DoesNotContain("Executed", html);
        Assert.Contains("Denied by policy.", html);
        Assert.Contains("No matched policy recorded", html);
        Assert.Contains("/audit/allow-final", html);
        Assert.Contains("/audit/legacy-deny", html);
        Assert.Contains("Distinct operations", html);
        Assert.True(html.IndexOf("/audit/rejected-lifecycle", StringComparison.Ordinal) <
            html.IndexOf("/audit/allow-final", StringComparison.Ordinal));
        Assert.Contains("release-&amp;-timeline", html);
        Assert.DoesNotContain(capability, html);
    }

    [Fact]
    public async Task SelectedCapability_FiltersPreserveCapabilityContext()
    {
        var capability = $"filtered-{Guid.NewGuid():N}";
        var activity = new InMemoryActivityStore();
        var audit = new InMemoryAuditEventStore();
        var approvals = new InMemoryApprovalStore();
        var allow = Event("allow-filtered", DateTimeOffset.UtcNow, capability,
            DecisionType.Allow, EnforcementMode.Enforce, operationId: "op-allow",
            reason: "Allowed.");
        var deny = Event("deny-filtered", DateTimeOffset.UtcNow.AddSeconds(-1), capability,
            DecisionType.Deny, EnforcementMode.LogOnly, operationId: "op-deny",
            reason: "Denied.");
        foreach (var item in new[] { allow, deny }) { await audit.WriteAsync(item); await activity.RecordAsync(item); }

        using var client = CreateClient(activity, audit, approvals);
        var url = $"/capability-activity?capabilityId={Uri.EscapeDataString(capability)}" +
            "&decision=Deny&identity=release-worker&environment=production" +
            "&operationId=op-deny&runtimeMode=LogOnly";
        var html = await client.GetStringAsync(url);

        Assert.Contains("value=\"Deny\" selected=\"selected\"", html);
        Assert.Contains("value=\"op-deny\" selected=\"selected\"", html);
        Assert.Contains("value=\"LogOnly\" selected=\"selected\"", html);
        Assert.Contains("deny-filtered", html);
        Assert.DoesNotContain("allow-filtered", html);
        Assert.Contains($"capabilityId={Uri.EscapeDataString(capability)}", html);
        Assert.Contains(
            $"/audit?capabilityId={Uri.EscapeDataString(capability)}&amp;identityId=release-worker&amp;environment=production&amp;enforcementMode=LogOnly&amp;decision=deny",
            html);
        Assert.Contains("View capability profile", html);
        Assert.Contains("href=\"/monitor\">Live Monitor</a> / Capability Activity", html);
    }

    [Fact]
    public async Task SelectedCapability_RendersIntentionalEmptyTimeline()
    {
        var capability = $"empty-timeline-{Guid.NewGuid():N}";
        var activity = new InMemoryActivityStore();
        var marker = Event("activity-only", DateTimeOffset.UtcNow, capability,
            DecisionType.Allow, EnforcementMode.LogOnly, reason: "Activity only.");
        await activity.RecordAsync(marker);
        using var client = CreateClient(activity, new InMemoryAuditEventStore(),
            new InMemoryApprovalStore());

        var html = await client.GetStringAsync(
            $"/capability-activity?capabilityId={Uri.EscapeDataString(capability)}");

        Assert.Contains("No recent activity", html);
        Assert.Contains("Evaluations will appear here", html);
        Assert.DoesNotContain("operation-timeline-group", html);
    }

    [Fact]
    public async Task CapabilityExplorer_UsesActivityAsPrimaryInvestigationAction()
    {
        var html = await _client.GetStringAsync(
            "/capability-explorer?capabilityId=production.deployment.execute");

        Assert.Contains("Investigate Capability Activity", html);
        Assert.Contains("/capability-activity?capabilityId=production.deployment.execute", html);
        Assert.DoesNotContain("Open full activity timeline", html);
    }

    [Fact]
    public async Task Summary_UsesSingularOperationAndEvaluationAttemptWording()
    {
        var html = await RenderTimelineAsync(Event("pending-singular",
            DateTimeOffset.UtcNow, $"singular-{Guid.NewGuid():N}",
            DecisionType.RequireApproval, EnforcementMode.Enforce,
            operationId: "operation-1", approvalStatus: "Pending",
            approvalAction: "Requested"));

        Assert.Contains("1 operation is awaiting approval after 1 evaluation attempt.", html);
        Assert.Contains(">Evaluation attempt<", html);
        Assert.Contains(">Distinct operation<", html);
        Assert.Contains(">Pending approval<", html);
    }

    [Fact]
    public async Task Summary_UsesPluralOperationWordingForCorrelatedAttempts()
    {
        var capability = $"plural-{Guid.NewGuid():N}";
        var html = await RenderTimelineAsync(
            Event("allow-one", DateTimeOffset.UtcNow, capability,
                DecisionType.Allow, EnforcementMode.Enforce,
                operationId: "operation-1"),
            Event("allow-two", DateTimeOffset.UtcNow.AddSeconds(-1), capability,
                DecisionType.Allow, EnforcementMode.Enforce,
                operationId: "operation-2"));

        Assert.Contains("2 Allow evaluation attempts across 2 operations.", html);
        Assert.Contains(">Evaluation attempts<", html);
        Assert.Contains(">Distinct operations<", html);
        Assert.Contains(">Allow decisions<", html);
    }

    [Fact]
    public async Task Summary_DescribesGroupedDenialsAsEvaluationAttempts()
    {
        var capability = $"denied-attempts-{Guid.NewGuid():N}";
        var html = await RenderTimelineAsync(
            Event("deny-one", DateTimeOffset.UtcNow, capability,
                DecisionType.Deny, EnforcementMode.Enforce,
                operationId: "same-operation"),
            Event("deny-two", DateTimeOffset.UtcNow.AddSeconds(-1), capability,
                DecisionType.Deny, EnforcementMode.Enforce,
                operationId: "same-operation"));

        Assert.Contains("2 denied evaluation attempts across 1 operation.", html);
        Assert.DoesNotContain("2 operations", html);
    }

    [Fact]
    public async Task Summary_DescribesLegacyEvaluationsWithoutInferringOperations()
    {
        var html = await RenderTimelineAsync(Event("legacy-only",
            DateTimeOffset.UtcNow, $"legacy-{Guid.NewGuid():N}",
            DecisionType.Deny, EnforcementMode.LogOnly));

        Assert.Contains("1 legacy evaluation without Operation IDs; distinct operations cannot be determined.", html);
        Assert.Contains(">Distinct operations<", html);
        Assert.DoesNotContain("across 1 operation", html);
    }

    private async Task<string> RenderTimelineAsync(params AuditEvent[] events)
    {
        var activity = new InMemoryActivityStore();
        var audit = new InMemoryAuditEventStore();
        foreach (var item in events)
        {
            await audit.WriteAsync(item);
            await activity.RecordAsync(item);
        }

        using var client = CreateClient(activity, audit, new InMemoryApprovalStore());
        return await client.GetStringAsync($"/capability-activity?capabilityId=" +
            Uri.EscapeDataString(events[0].CapabilityId));
    }

    private HttpClient CreateClient(IActivityStore activity,
        IAuditEventStore audit, IApprovalStore approvals) => _factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IActivityStore>();
            services.RemoveAll<IAuditEventStore>();
            services.RemoveAll<IAuditSink>();
            services.RemoveAll<IApprovalStore>();
            services.AddSingleton(activity);
            services.AddSingleton<IActivityStore>(activity);
            services.AddSingleton(audit);
            services.AddSingleton<IAuditEventStore>(audit);
            services.AddSingleton<IAuditSink>(audit);
            services.AddSingleton(approvals);
            services.AddSingleton<IApprovalStore>(approvals);
        })).CreateClient();

    private static AuditEvent Event(string id, DateTimeOffset timestamp,
        string capability, DecisionType decision, EnforcementMode mode,
        string? operationId = null, string? approvalId = null,
        string? approvalStatus = null, string? approvalAction = null,
        string reason = "Recorded.") => new()
        {
            Id = id,
            RequestId = $"request-{id}",
            TimestampUtc = timestamp,
            IdentityId = "release-worker",
            CapabilityId = capability,
            Environment = "production",
            ResourceId = "checkout-api",
            Decision = decision,
            PolicyDecision = decision,
            EnforcementMode = mode,
            MatchedPolicies = id.Contains("legacy", StringComparison.Ordinal) ? [] : ["release-policy"],
            Reason = reason,
            PolicyReason = reason,
            ApprovalId = approvalId,
            ApprovalStatus = approvalStatus,
            ApprovalAction = approvalAction,
            ApprovalOperationId = operationId,
            ExecutionGuidance = decision == DecisionType.RequireApproval ? "Pause" :
                decision == DecisionType.Deny && mode == EnforcementMode.LogOnly
                    ? "ContinueLogOnly" : decision == DecisionType.Deny ? "Block" : "Proceed"
        };
    [Fact]
    public async Task CapabilityActivity_ShowsCatalogContextWithoutObservedActivity()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IActivityStore>();
                services.AddSingleton<IActivityStore, InMemoryActivityStore>();
            });
        }).CreateClient();

        var html = await client.GetStringAsync(
            "/capability-activity?capabilityId=DeployApplication");

        Assert.Contains("Deploy Application", html);
        Assert.Contains("Medium risk", html);
        Assert.Contains("Source: Local catalog", html);
        Assert.Contains("No observed activity", html);
        Assert.DoesNotContain("View Decision Trace", html);
    }
}
