using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class HumanApprovalDecisionTests
{
    [Fact]
    public async Task ApprovedApprovalIsConsumedThenNextRetryCreatesPending()
    {
        var approvals = new InMemoryApprovalStore();
        var audit = new InMemoryAuditEventStore();
        var service = new CoreDecisionService(
            new PolicyLoader(), new Seneschal.Core.Services.PolicyEvaluator(),
            new InMemoryGovernanceModeStore(new RuntimeSettings { Mode = EnforcementMode.Enforce }),
            audit, approvalStore: approvals);
        var request = Request("vault-a");

        var pending = service.Evaluate(request);
        var record = Assert.Single(approvals.GetAll());
        approvals.Resolve(record.Id, ApprovalStatus.Approved, "reviewer", DateTimeOffset.UtcNow);
        var resolved = service.Evaluate(request);
        var next = service.Evaluate(request);

        Assert.Equal("requires_approval", pending.Decision);
        Assert.Equal("allow", resolved.Decision);
        Assert.Equal("requires_approval", next.Decision);
        Assert.Equal(2, approvals.GetAll().Count);
        var consumed = approvals.GetAll().Single(item => item.Id == record.Id);
        Assert.Equal(ApprovalStatus.Consumed, consumed.Status);
        var evidence = (await audit.GetRecentAsync()).Single(item => item.Id == consumed.ConsumedByDecisionId);
        Assert.Equal(record.Id, evidence.ApprovalId);
        Assert.Equal("Consumed", evidence.ApprovalAction);
        Assert.Equal(evidence.Id, evidence.ApprovalConsumedByDecisionId);
        Assert.NotNull(evidence.ApprovalConsumedAt);
        Assert.Equal("reviewer", evidence.ApprovalResolvedBy);
        Assert.Equal(DecisionType.RequireApproval, evidence.PolicyDecision);
    }

    [Fact]
    public void RejectedApprovalRemainsDeny()
    {
        var approvals = new InMemoryApprovalStore();
        var service = CreateService(approvals);
        service.Evaluate(Request("vault-a"));
        var record = Assert.Single(approvals.GetAll());
        approvals.Resolve(record.Id, ApprovalStatus.Rejected, "reviewer", DateTimeOffset.UtcNow);
        Assert.Equal("deny", service.Evaluate(Request("vault-a")).Decision);
        Assert.Equal("deny", service.Evaluate(Request("vault-a")).Decision);
        Assert.Single(approvals.GetAll());
    }

    [Fact]
    public void DifferentResourceDoesNotUseApprovedRecord()
    {
        var approvals = new InMemoryApprovalStore();
        var service = new CoreDecisionService(
            new PolicyLoader(), new Seneschal.Core.Services.PolicyEvaluator(),
            new InMemoryGovernanceModeStore(new RuntimeSettings()),
            approvalStore: approvals);
        service.Evaluate(Request("vault-a"));
        var record = Assert.Single(approvals.GetAll());
        approvals.Resolve(record.Id, ApprovalStatus.Approved, "reviewer", DateTimeOffset.UtcNow);
        Assert.Equal("requires_approval", service.Evaluate(Request("vault-b")).Decision);
        Assert.Equal(2, approvals.GetAll().Count);
        Assert.Equal(ApprovalStatus.Approved,
            approvals.GetAll().Single(item => item.Id == record.Id).Status);
    }

    [Fact]
    public void GovernanceWindowCanOverrideApprovalDerivedAllow()
    {
        var approvals = new InMemoryApprovalStore();
        var service = new CoreDecisionService(
            new PolicyLoader(), new Seneschal.Core.Services.PolicyEvaluator(),
            new InMemoryGovernanceModeStore(new RuntimeSettings()),
            governanceWindowStore: new SecretWindowStore(), approvalStore: approvals);
        service.Evaluate(Request("vault-a"));
        var record = Assert.Single(approvals.GetAll());
        approvals.Resolve(record.Id, ApprovalStatus.Approved, "reviewer", DateTimeOffset.UtcNow);
        var result = service.Evaluate(Request("vault-a"));
        Assert.Equal("deny", result.Decision);
        Assert.Equal(ApprovalStatus.Consumed, approvals.GetAll().Single().Status);
    }

    [Fact]
    public void OperationScopedApprovalOnlyMatchesSameOperationId()
    {
        var approvals = new InMemoryApprovalStore();
        var service = CreateService(approvals);
        var first = service.Evaluate(Request("vault-a", "release-001"));
        var record = Assert.Single(approvals.GetAll());
        var duplicate = service.Evaluate(Request("vault-a", "release-001"));
        Assert.Equal(first.ApprovalId, duplicate.ApprovalId);

        approvals.Resolve(record.Id, ApprovalStatus.Approved, "reviewer", DateTimeOffset.UtcNow);
        var allowed = service.Evaluate(Request("vault-a", "release-001"));
        var different = service.Evaluate(Request("vault-a", "release-002"));
        var missing = service.Evaluate(Request("vault-a"));

        Assert.Equal("allow", allowed.Decision);
        Assert.Equal("release-001", allowed.OperationId);
        Assert.Equal("requires_approval", different.Decision);
        Assert.Equal("requires_approval", missing.Decision);
        Assert.NotEqual(record.Id, different.ApprovalId);
        Assert.NotEqual(record.Id, missing.ApprovalId);
        Assert.Equal(3, approvals.GetAll().Count);
        Assert.Equal(ApprovalStatus.Consumed,
            approvals.GetAll().Single(item => item.Id == record.Id).Status);
    }

    private static CoreDecisionService CreateService(InMemoryApprovalStore approvals) =>
        new(new PolicyLoader(), new Seneschal.Core.Services.PolicyEvaluator(),
            new InMemoryGovernanceModeStore(new RuntimeSettings()),
            approvalStore: approvals);

    private sealed class SecretWindowStore : Seneschal.Core.Interfaces.IGovernanceWindowStore
    {
        public Seneschal.Core.Models.GovernanceWindow GetWindow() => new()
        {
            Name = "Secret Freeze", Description = "test", Enabled = true,
            Mode = GovernanceWindowMode.Enforce,
            AffectedCapabilities = ["azure.keyvault.secret.read"], Reason = "test"
        };
        public void SetState(bool enabled, GovernanceWindowMode mode) { }
    }

    private static DecisionRequest Request(string resource, string? operationId = null) => new()
    {
        Identity = "SupportAgent",
        Capability = "azure.keyvault.secret.read",
        OperationId = operationId,
        Context = new() { ["environment"] = "prod", ["resource"] = resource, ["reason"] = "Customer incident" }
    };
}
