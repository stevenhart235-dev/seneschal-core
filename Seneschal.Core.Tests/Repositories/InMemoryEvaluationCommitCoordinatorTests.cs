using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryEvaluationCommitCoordinatorTests
{
    [Fact]
    public async Task Commit_AppendsEvidenceAndCreatesApprovalAtomically()
    {
        var evidenceStore = new InMemoryAuditEventStore();
        var approvalStore = new InMemoryApprovalStore();
        var coordinator = new InMemoryEvaluationCommitCoordinator(
            evidenceStore,
            approvalStore);
        var approval = CreateApproval();
        var commit = CreateCommit(approval);

        await coordinator.CommitAsync(commit);

        Assert.Single(await evidenceStore.GetRecentAsync());
        Assert.Equal(approval, Assert.Single(approvalStore.GetAll()));
    }

    [Fact]
    public async Task Commit_IdenticalRetryIsIdempotentAcrossBothEffects()
    {
        var evidenceStore = new InMemoryAuditEventStore();
        var approvalStore = new InMemoryApprovalStore();
        var coordinator = new InMemoryEvaluationCommitCoordinator(
            evidenceStore,
            approvalStore);
        var commit = CreateCommit(CreateApproval());

        await coordinator.CommitAsync(commit);
        await coordinator.CommitAsync(commit);

        Assert.Single(await evidenceStore.GetRecentAsync());
        Assert.Single(approvalStore.GetAll());
    }

    [Fact]
    public async Task Commit_ConflictingEvidenceLeavesApprovalUnchanged()
    {
        var evidenceStore = new InMemoryAuditEventStore();
        var approvalStore = new InMemoryApprovalStore();
        var coordinator = new InMemoryEvaluationCommitCoordinator(
            evidenceStore,
            approvalStore);
        await evidenceStore.WriteAsync(CreateEvidence("Original."));

        await Assert.ThrowsAsync<EvaluationEvidenceConflictException>(() =>
            coordinator.CommitAsync(CreateCommit(
                CreateApproval(),
                "Conflicting.")));

        Assert.Empty(approvalStore.GetAll());
        Assert.Single(await evidenceStore.GetRecentAsync());
    }

    private static EvaluationCommit CreateCommit(
        ApprovalRecord approval,
        string reason = "Original.")
    {
        return new EvaluationCommit
        {
            Evidence = CreateEvidence(reason),
            ApprovalMutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Create,
                Record = approval
            }
        };
    }

    private static ApprovalRecord CreateApproval()
    {
        return new ApprovalRecord
        {
            Id = "approval-1",
            IdentityId = "SupportAgent",
            CapabilityId = "azure.keyvault.secret.read",
            Environment = "prod",
            ResourceId = "vault-a",
            RequestReason = "Review required.",
            RequestedAt = Timestamp
        };
    }

    private static AuditEvent CreateEvidence(string reason)
    {
        return new AuditEvent
        {
            Id = "decision-1",
            TimestampUtc = Timestamp,
            IdentityId = "SupportAgent",
            CapabilityId = "azure.keyvault.secret.read",
            ResourceId = "vault-a",
            Environment = "prod",
            Decision = DecisionType.RequireApproval,
            EnforcementMode = EnforcementMode.Enforce,
            EffectiveAction = "requires_approval",
            Reason = reason,
            ApprovalId = "approval-1",
            ApprovalStatus = ApprovalStatus.Pending.ToString()
        };
    }

    private static readonly DateTimeOffset Timestamp =
        new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
}
