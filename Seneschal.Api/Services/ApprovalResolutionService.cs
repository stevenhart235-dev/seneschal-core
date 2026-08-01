using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Services;

public sealed class ApprovalResolutionService(
    IApprovalStore approvalStore,
    IEvaluationCommitCoordinator commitCoordinator,
    IGovernanceModeStore modeStore)
{
    public async Task<ApprovalRecord?> ResolveAsync(
        string approvalId,
        ApprovalStatus status,
        string resolvedBy,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default)
    {
        if (status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected) ||
            string.IsNullOrWhiteSpace(resolvedBy))
        {
            return null;
        }
        var current = approvalStore.GetById(approvalId);
        if (current is null)
        {
            return null;
        }
        if (current.Status != ApprovalStatus.Pending)
        {
            throw new Seneschal.Core.Exceptions.ApprovalTransitionException(
                approvalId, current.Status, status);
        }
        var resolved = current with
        {
            Status = status,
            ResolvedAt = resolvedAt,
            ResolvedBy = resolvedBy.Trim()
        };
        var evidence = new AuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            RequestId = resolved.Id,
            TimestampUtc = resolvedAt,
            IdentityId = resolved.IdentityId,
            CapabilityId = resolved.CapabilityId,
            ResourceId = resolved.ResourceId,
            Environment = resolved.Environment,
            Decision = status == ApprovalStatus.Approved
                ? DecisionType.Allow : DecisionType.Deny,
            EffectiveAction = "approval_" + status.ToString().ToLowerInvariant(),
            PolicyDecision = DecisionType.RequireApproval,
            EnforcementMode = modeStore.GetMode(),
            Reason = $"Approval {status.ToString().ToLowerInvariant()} by {resolved.ResolvedBy}.",
            PolicyReason = resolved.RequestReason,
            ApprovalId = resolved.Id,
            ApprovalStatus = resolved.Status.ToString(),
            ApprovalAction = resolved.Status.ToString(),
            ApprovalRequestReason = resolved.RequestReason,
            ApprovalResolvedAt = resolved.ResolvedAt,
            ApprovalResolvedBy = resolved.ResolvedBy,
            ApprovalOperationId = resolved.OperationId,
            ApprovalCorrelationMode = resolved.CorrelationMode.ToString()
        };
        await commitCoordinator.CommitAsync(new EvaluationCommit
        {
            Evidence = evidence,
            ApprovalMutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Resolve,
                Record = resolved,
                ExpectedStatus = ApprovalStatus.Pending
            }
        }, cancellationToken);
        return resolved;
    }
}
