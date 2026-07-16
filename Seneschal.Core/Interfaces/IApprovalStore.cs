using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IApprovalStore
{
    ApprovalLookupResult GetOrCreate(
        string identityId, string capabilityId, string environment,
        string resourceId, string requestReason, DateTimeOffset requestedAt,
        string? operationId = null);
    ApprovalRecord? Find(
        string identityId, string capabilityId, string environment,
        string resourceId, string? operationId = null);
    ApprovalRecord? Resolve(
        string approvalId, ApprovalStatus status, string resolvedBy,
        DateTimeOffset resolvedAt);
    ApprovalRecord? Consume(
        string approvalId, string decisionId, DateTimeOffset consumedAt);
    IReadOnlyCollection<ApprovalRecord> GetAll();
}
