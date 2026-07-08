using System.Diagnostics;
using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using CoreEnforcementMode = Seneschal.Core.Enums.EnforcementMode;
using CorePolicyEvaluator = Seneschal.Core.Services.PolicyEvaluator;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class CoreDecisionServiceTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly CoreDecisionService _service;

    public CoreDecisionServiceTests(ApiApplicationFactory factory)
    {
        _ = factory;
        _service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            });
    }

    [Fact]
    public void Evaluate_MapsAllowDecision()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        Assert.Equal("allow", result.Decision);
        Assert.Equal("allow", result.EffectiveAction);
        Assert.Equal("LogOnly", result.Mode);
        Assert.True(result.DurationMs >= 0);
    }

    [Fact]
    public void Evaluate_MapsCompatibilityDefaultDenyFallback()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "UnknownIdentity",
                "UnknownCapability",
                "dev"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal("default-deny", result.PolicyMatched);
        Assert.Equal("No matching allow policy found", result.Reason);
    }

    [Fact]
    public void Evaluate_MapsWinningPolicyName()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeleteProductionDatabase",
                "prod"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal(
            "Developers cannot delete production databases",
            result.PolicyMatched);
    }

    [Fact]
    public void Evaluate_MapsRequiresApprovalDecision()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "SupportAgent",
                "azure.keyvault.secret.read",
                "prod"));

        Assert.Equal("requires_approval", result.Decision);
        Assert.Equal(
            "Support secret reads require approval",
            result.PolicyMatched);
    }

    [Fact]
    public void Evaluate_ProjectsNonAllowDecisionAsLoggedOnly()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeleteProductionDatabase",
                "prod"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal("logged_only", result.EffectiveAction);
        Assert.Equal("LogOnly", result.Mode);
    }

    [Fact]
    public async Task Evaluate_WritesAuditEventWhenAuditSinkIsRegistered()
    {
        var auditStore = new InMemoryAuditEventStore();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            auditStore);

        var result = service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        var auditEvent = Assert.Single(
            await auditStore.GetRecentAsync());
        Assert.Equal("allow", result.Decision);
        Assert.Equal("Developer", auditEvent.IdentityId);
        Assert.Equal("DeployApplication", auditEvent.CapabilityId);
        Assert.Equal("dev", auditEvent.Environment);
        Assert.Equal("allow", result.Decision);
        Assert.Contains(
            "Developers can deploy to dev",
            auditEvent.MatchedPolicies);
        Assert.Empty(auditEvent.Obligations);
        Assert.True(auditEvent.EvaluationDurationMs >= 0);
    }

    [Fact]
    public async Task Evaluate_RecordsActivityWhenActivityStoreIsRegistered()
    {
        var activityStore = new InMemoryActivityStore();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            activityStore: activityStore);

        var result = service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        var snapshot = await activityStore.GetSnapshotAsync();
        var capability = Assert.Single(snapshot.Capabilities);
        var identity = Assert.Single(snapshot.Identities);
        var policy = snapshot.Policies.Single(policy =>
            policy.PolicyId == "Developers can deploy to dev");

        Assert.Equal("allow", result.Decision);
        Assert.Equal("DeployApplication", capability.CapabilityId);
        Assert.Equal(1, capability.TotalRequests);
        Assert.Equal(1, capability.AllowedCount);
        Assert.Equal("Developer", identity.IdentityId);
        Assert.Equal(1, identity.TotalRequests);
        Assert.Contains("DeployApplication", identity.DistinctCapabilitiesUsed);
        Assert.Equal("Developers can deploy to dev", policy.PolicyId);
        Assert.Equal(1, policy.MatchCount);
    }

    [Fact]
    public async Task Evaluate_ExportsDecisionEventWhenExporterIsRegistered()
    {
        var exporter = new InMemoryDecisionExporter();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            decisionExporter: exporter);

        var result = service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        var export = Assert.Single(await exporter.GetExportsAsync());
        Assert.Equal("allow", result.Decision);
        Assert.Equal("Developer", export.Identity);
        Assert.Equal("DeployApplication", export.Capability);
        Assert.Equal("dev", export.Environment);
        Assert.Equal("Allow", export.Decision);
        Assert.Equal("Developers can deploy to dev", export.MatchedPolicy);
        Assert.True(export.EvaluationDurationMs >= 0);
        Assert.Equal(
            "Developer is allowed to deploy applications to dev",
            export.Reason);
    }

    [Fact]
    public async Task Evaluate_ExportsIndependentlyFromAuditAndActivity()
    {
        var exporter = new InMemoryDecisionExporter();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            decisionExporter: exporter);

        service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        Assert.Single(await exporter.GetExportsAsync());
    }

    [Fact]
    public async Task Evaluate_ExporterFailureDoesNotPreventAuditOrActivity()
    {
        var auditStore = new InMemoryAuditEventStore();
        var activityStore = new InMemoryActivityStore();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            auditStore,
            activityStore,
            new ThrowingDecisionExporter());

        var result = service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        Assert.Equal("allow", result.Decision);
        Assert.Single(await auditStore.GetRecentAsync());
        Assert.Single((await activityStore.GetSnapshotAsync()).Capabilities);
    }

    [Fact]
    public async Task Evaluate_MetricsFailureDoesNotPreventAuditActivityOrExport()
    {
        var auditStore = new InMemoryAuditEventStore();
        var activityStore = new InMemoryActivityStore();
        var exporter = new InMemoryDecisionExporter();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            auditStore,
            activityStore,
            exporter,
            new ThrowingDecisionMetrics());

        var result = service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        Assert.Equal("allow", result.Decision);
        Assert.Single(await auditStore.GetRecentAsync());
        Assert.Single((await activityStore.GetSnapshotAsync()).Capabilities);
        Assert.Single(await exporter.GetExportsAsync());
    }

    [Fact]
    public async Task Evaluate_RecordsDecisionMetricsWhenMetricsAreRegistered()
    {
        var metrics = new InMemoryDecisionMetrics();
        var service = new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new RuntimeSettings
            {
                Mode = CoreEnforcementMode.LogOnly
            },
            decisionMetrics: metrics);

        service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        var rendered = metrics.RenderPrometheus();

        Assert.Contains("seneschal_decisions_total 1", rendered);
        Assert.Contains("seneschal_decisions_allowed_total 1", rendered);
        Assert.Contains(
            "seneschal_capability_decisions_total{capability=\"DeployApplication\"} 1",
            rendered);
    }

    [Fact]
    public void Evaluate_CreatesDecisionActivityWhenListenerIsRegistered()
    {
        using var listener = CreateDecisionActivityListener(out var activities);

        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        var activity = Assert.Single(activities);
        var tags = activity.TagObjects.ToDictionary(
            tag => tag.Key,
            tag => tag.Value);

        Assert.Equal("allow", result.Decision);
        Assert.Equal("seneschal.evaluate", activity.DisplayName);
        Assert.Equal(
            CoreDecisionService.DecisionActivitySourceName,
            activity.Source.Name);
        Assert.Equal("Developer", tags["seneschal.identity_id"]);
        Assert.Equal("DeployApplication", tags["seneschal.capability_id"]);
        Assert.Equal("dev", tags["seneschal.environment"]);
        Assert.Equal("contract-test-resource", tags["seneschal.resource_id"]);
        Assert.Equal("Allow", tags["seneschal.decision"]);
        Assert.Equal("LogOnly", tags["seneschal.enforcement_mode"]);
        Assert.True((int)tags["seneschal.matched_policy_count"]! >= 1);
        Assert.Equal(0, tags["seneschal.obligation_count"]);
        Assert.True((int)tags["seneschal.evaluation_duration_ms"]! >= 0);
        Assert.Contains(
            "Developers can deploy to dev",
            tags["seneschal.matched_policies"]?.ToString());
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public void Evaluate_DenyDecisionMarksActivityAsError()
    {
        using var listener = CreateDecisionActivityListener(out var activities);

        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeleteProductionDatabase",
                "prod"));

        var activity = Assert.Single(activities);
        var tags = activity.TagObjects.ToDictionary(
            tag => tag.Key,
            tag => tag.Value);

        Assert.Equal("deny", result.Decision);
        Assert.Equal("Deny", tags["seneschal.decision"]);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("Deny", activity.StatusDescription);
    }

    [Fact]
    public void Evaluate_PendingApprovalDecisionMarksActivityAsError()
    {
        using var listener = CreateDecisionActivityListener(out var activities);

        var result = _service.Evaluate(
            CreateRequest(
                "SupportAgent",
                "azure.keyvault.secret.read",
                "prod"));

        var activity = Assert.Single(activities);
        var tags = activity.TagObjects.ToDictionary(
            tag => tag.Key,
            tag => tag.Value);

        Assert.Equal("requires_approval", result.Decision);
        Assert.Equal("RequireApproval", tags["seneschal.decision"]);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("RequireApproval", activity.StatusDescription);
    }

    [Fact]
    public void Evaluate_SucceedsWithoutActivityListener()
    {
        var result = _service.Evaluate(
            CreateRequest(
                "Developer",
                "DeployApplication",
                "dev"));

        Assert.Equal("allow", result.Decision);
    }

    private static ApiDecisionRequest CreateRequest(
        string identity,
        string capability,
        string environment)
    {
        return new ApiDecisionRequest
        {
            Identity = identity,
            Capability = capability,
            Context = new Dictionary<string, string>
            {
                ["environment"] = environment,
                ["resource"] = "contract-test-resource"
            }
        };
    }

    private sealed class ThrowingDecisionExporter : IDecisionExporter
    {
        public Task ExportAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Export failed.");
        }
    }

    private sealed class ThrowingDecisionMetrics : IDecisionMetrics
    {
        public Task RecordAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Metrics failed.");
        }
    }

    private static ActivityListener CreateDecisionActivityListener(
        out List<Activity> activities)
    {
        var capturedActivities = new List<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == CoreDecisionService.DecisionActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capturedActivities.Add(activity)
        };

        ActivitySource.AddActivityListener(listener);
        activities = capturedActivities;

        return listener;
    }
}
