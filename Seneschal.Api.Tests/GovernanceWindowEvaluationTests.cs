using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using CorePolicyEvaluator = Seneschal.Core.Services.PolicyEvaluator;
using ApiDecisionRequest = Seneschal.Api.Models.DecisionRequest;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GovernanceWindowEvaluationTests
{
    [Fact]
    public async Task Observe_LeavesAllowUnchangedAndRecordsParticipation()
    {
        var windowStore = EnabledWindow(GovernanceWindowMode.Observe);
        var auditStore = new InMemoryAuditEventStore();
        var activityStore = new InMemoryActivityStore();
        var service = CreateService(windowStore, auditStore, activityStore);

        var result = service.Evaluate(Request(
            "deployment-worker",
            "production.deployment.execute"));

        Assert.Equal("allow", result.Decision);
        var auditEvent = Assert.Single(await auditStore.GetRecentAsync());
        Assert.Equal("Production Freeze", auditEvent.GovernanceWindowName);
        Assert.Equal("Observe", auditEvent.GovernanceWindowMode);
        Assert.Equal(
            "Governance Window matched: Production Freeze",
            auditEvent.GovernanceWindowMessage);
        var activity = Assert.Single(
            (await activityStore.GetSnapshotAsync()).Capabilities);
        Assert.Equal(1, activity.GovernedEvaluationCount);
    }

    [Fact]
    public async Task Enforce_OverridesAllowWithDenyAndRecordsReason()
    {
        var auditStore = new InMemoryAuditEventStore();
        var service = CreateService(
            EnabledWindow(GovernanceWindowMode.Enforce),
            auditStore);

        var result = service.Evaluate(Request(
            "deployment-worker",
            "production.deployment.execute"));

        Assert.Equal("deny", result.Decision);
        Assert.Equal("logged_only", result.EffectiveAction);
        Assert.Equal(
            "Blocked by Governance Window: Production Freeze",
            result.Reason);
        Assert.Equal(
            "Enforce",
            Assert.Single(await auditStore.GetRecentAsync()).GovernanceWindowMode);
    }

    [Fact]
    public async Task UnaffectedCapability_DoesNotRecordWindowParticipation()
    {
        var auditStore = new InMemoryAuditEventStore();
        var service = CreateService(
            EnabledWindow(GovernanceWindowMode.Enforce),
            auditStore);

        var result = service.Evaluate(Request(
            "refund-worker",
            "payments.refund.create"));

        Assert.Equal("allow", result.Decision);
        Assert.Null(Assert.Single(await auditStore.GetRecentAsync()).GovernanceWindowName);
    }

    private static InMemoryGovernanceWindowStore EnabledWindow(
        GovernanceWindowMode mode)
    {
        var store = new InMemoryGovernanceWindowStore();
        store.SetState(true, mode);
        return store;
    }

    private static CoreDecisionService CreateService(
        IGovernanceWindowStore windowStore,
        IAuditSink auditSink,
        IActivityStore? activityStore = null)
    {
        return new CoreDecisionService(
            new PolicyLoader(),
            new CorePolicyEvaluator(),
            new InMemoryGovernanceModeStore(new RuntimeSettings
            {
                Mode = EnforcementMode.LogOnly
            }),
            auditSink,
            activityStore,
            governanceWindowStore: windowStore);
    }

    private static ApiDecisionRequest Request(string identity, string capability)
    {
        return new ApiDecisionRequest
        {
            Identity = identity,
            Capability = capability,
            Context = new Dictionary<string, string>
            {
                ["environment"] = "production",
                ["resource"] = "governance-window-test"
            }
        };
    }
}
