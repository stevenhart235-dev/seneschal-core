using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class HumanApprovalDecisionTests
{
    [Theory]
    [InlineData(ApprovalStatus.Approved, "allow")]
    [InlineData(ApprovalStatus.Rejected, "deny")]
    public async Task ResolvedApprovalChangesExactRetryAndWritesEvidence(
        ApprovalStatus status, string expectedDecision)
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
        approvals.Resolve(record.Id, status, "reviewer", DateTimeOffset.UtcNow);
        var resolved = service.Evaluate(request);

        Assert.Equal("requires_approval", pending.Decision);
        Assert.Equal(expectedDecision, resolved.Decision);
        var evidence = (await audit.GetRecentAsync()).First();
        Assert.Equal(record.Id, evidence.ApprovalId);
        Assert.Equal("Used", evidence.ApprovalAction);
        Assert.Equal("reviewer", evidence.ApprovalResolvedBy);
        Assert.Equal(DecisionType.RequireApproval, evidence.PolicyDecision);
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
    }

    private static DecisionRequest Request(string resource) => new()
    {
        Identity = "SupportAgent",
        Capability = "azure.keyvault.secret.read",
        Context = new() { ["environment"] = "prod", ["resource"] = resource, ["reason"] = "Customer incident" }
    };
}
