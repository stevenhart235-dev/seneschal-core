using Seneschal.Core.Enums;

namespace Seneschal.Core.Exceptions;

public sealed class ApprovalTransitionException : Exception
{
    public ApprovalTransitionException(
        string approvalId,
        ApprovalStatus currentStatus,
        ApprovalStatus requestedStatus) : base(
            $"Approval '{approvalId}' cannot transition from {currentStatus} to {requestedStatus}.")
    {
        ApprovalId = approvalId;
        CurrentStatus = currentStatus;
        RequestedStatus = requestedStatus;
    }

    public string ApprovalId { get; }
    public ApprovalStatus CurrentStatus { get; }
    public ApprovalStatus RequestedStatus { get; }
}
