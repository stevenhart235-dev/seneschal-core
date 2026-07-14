using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IApprovalStore
{
    ApprovalLookupResult GetOrCreate(
        string identityId, string capabilityId, string environment,
        string resourceId, string requestReason, DateTimeOffset requestedAt);
    ApprovalRecord? Find(
        string identityId, string capabilityId, string environment,
        string resourceId);
    ApprovalRecord? Resolve(
        string approvalId, ApprovalStatus status, string resolvedBy,
        DateTimeOffset resolvedAt);
    IReadOnlyCollection<ApprovalRecord> GetAll();
}
