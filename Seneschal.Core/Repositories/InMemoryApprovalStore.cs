using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly Dictionary<string, ApprovalRecord> _records =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public ApprovalLookupResult GetOrCreate(
        string identityId, string capabilityId, string environment,
        string resourceId, string requestReason, DateTimeOffset requestedAt,
        string? operationId = null)
    {
        operationId = NormalizeOperationId(operationId);
        var scope = ScopeKey(identityId, capabilityId, environment, resourceId, operationId);
        lock (_gate)
        {
            var existing = _records.Values
                .Where(record => ScopeKey(record.IdentityId, record.CapabilityId,
                    record.Environment, record.ResourceId, record.OperationId).Equals(scope, StringComparison.OrdinalIgnoreCase) &&
                    record.Status != ApprovalStatus.Consumed)
                .OrderByDescending(record => record.RequestedAt)
                .FirstOrDefault();
            if (existing is not null)
                return new ApprovalLookupResult(existing, false);

            var record = new ApprovalRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                IdentityId = identityId,
                CapabilityId = capabilityId,
                Environment = environment,
                ResourceId = resourceId,
                OperationId = operationId,
                CorrelationMode = operationId is null
                    ? ApprovalCorrelationMode.LegacyContext
                    : ApprovalCorrelationMode.Operation,
                RequestReason = requestReason,
                RequestedAt = requestedAt
            };
            _records[record.Id] = record;
            return new ApprovalLookupResult(record, true);
        }
    }

    public ApprovalRecord? Find(
        string identityId, string capabilityId, string environment,
        string resourceId, string? operationId = null)
    {
        lock (_gate)
        {
            operationId = NormalizeOperationId(operationId);
            var scope = ScopeKey(identityId, capabilityId, environment, resourceId, operationId);
            return _records.Values
                .Where(record => ScopeKey(record.IdentityId, record.CapabilityId,
                    record.Environment, record.ResourceId, record.OperationId).Equals(scope, StringComparison.OrdinalIgnoreCase) &&
                    record.Status != ApprovalStatus.Consumed)
                .OrderByDescending(record => record.RequestedAt)
                .FirstOrDefault();
        }
    }

    public ApprovalRecord? Resolve(
        string approvalId, ApprovalStatus status, string resolvedBy,
        DateTimeOffset resolvedAt)
    {
        if (status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected) ||
            string.IsNullOrWhiteSpace(resolvedBy))
            return null;
        lock (_gate)
        {
            if (!_records.TryGetValue(approvalId, out var record) ||
                record.Status != ApprovalStatus.Pending)
                return null;
            var resolved = record with
            {
                Status = status,
                ResolvedAt = resolvedAt,
                ResolvedBy = resolvedBy.Trim()
            };
            _records[approvalId] = resolved;
            return resolved;
        }
    }

    public ApprovalRecord? Consume(
        string approvalId, string decisionId, DateTimeOffset consumedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        lock (_gate)
        {
            if (!_records.TryGetValue(approvalId, out var record) ||
                record.Status != ApprovalStatus.Approved)
                return null;

            var consumed = record with
            {
                Status = ApprovalStatus.Consumed,
                ConsumedAt = consumedAt,
                ConsumedByDecisionId = decisionId
            };
            _records[approvalId] = consumed;
            return consumed;
        }
    }

    public IReadOnlyCollection<ApprovalRecord> GetAll()
    {
        lock (_gate) return _records.Values.ToList();
    }

    private static string ScopeKey(string identity, string capability,
        string environment, string resource, string? operationId) =>
        string.Join('\u001f', identity, capability, environment, resource,
            operationId is null ? "legacy-context" : $"operation:{operationId}");

    private static string? NormalizeOperationId(string? operationId) =>
        string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
}
