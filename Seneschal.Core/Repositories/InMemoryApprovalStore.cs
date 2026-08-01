using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly Dictionary<string, ApprovalRecord> _records =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    internal object SyncRoot => _gate;

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

    public ApprovalRecord? GetById(string approvalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        lock (_gate)
        {
            return _records.GetValueOrDefault(approvalId);
        }
    }

    public IReadOnlyCollection<ApprovalRecord> GetPending()
    {
        lock (_gate)
        {
            return Order(_records.Values.Where(record =>
                record.Status == ApprovalStatus.Pending));
        }
    }

    public IReadOnlyCollection<ApprovalRecord> GetHistory()
    {
        lock (_gate)
        {
            return Order(_records.Values.Where(record =>
                record.Status != ApprovalStatus.Pending));
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
            if (!_records.TryGetValue(approvalId, out var record))
                return null;
            if (record.Status != ApprovalStatus.Pending)
                throw new ApprovalTransitionException(
                    approvalId, record.Status, status);
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
            if (!_records.TryGetValue(approvalId, out var record))
                return null;
            if (record.Status != ApprovalStatus.Approved)
                throw new ApprovalTransitionException(
                    approvalId, record.Status, ApprovalStatus.Consumed);

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
        lock (_gate) return Order(_records.Values);
    }

    internal ApprovalMutation? PrepareMutationNoLock(
        ApprovalMutation? mutation)
    {
        if (mutation is null)
        {
            return null;
        }

        var target = mutation.Record;
        if (_records.TryGetValue(target.Id, out var existing) &&
            existing == target)
        {
            return null;
        }

        if (mutation.Kind == ApprovalMutationKind.Create)
        {
            if (existing is not null)
            {
                throw new EvaluationCommitException(
                    $"Approval '{target.Id}' already exists with different content.");
            }

            var scope = ScopeKey(
                target.IdentityId,
                target.CapabilityId,
                target.Environment,
                target.ResourceId,
                target.OperationId);
            var conflictingScope = _records.Values.Any(record =>
                record.Status != ApprovalStatus.Consumed &&
                ScopeKey(
                    record.IdentityId,
                    record.CapabilityId,
                    record.Environment,
                    record.ResourceId,
                    record.OperationId).Equals(
                        scope,
                        StringComparison.OrdinalIgnoreCase));

            if (conflictingScope)
            {
                throw new EvaluationCommitException(
                    "Approval scope changed before the evaluation could commit.");
            }

            return mutation;
        }

        if (mutation.Kind == ApprovalMutationKind.Consume)
        {
            if (existing is null ||
                existing.Status != mutation.ExpectedStatus ||
                target.Status != ApprovalStatus.Consumed)
            {
                throw new EvaluationCommitException(
                    $"Approval '{target.Id}' could not be consumed atomically.");
            }

            return mutation;
        }

        if (mutation.Kind == ApprovalMutationKind.Resolve)
        {
            if (existing is null ||
                existing.Status != ApprovalStatus.Pending ||
                target.Status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
            {
                throw new EvaluationCommitException(
                    $"Approval '{target.Id}' could not be resolved atomically.");
            }

            return mutation;
        }

        throw new EvaluationCommitException(
            $"Unsupported approval mutation '{mutation.Kind}'.");
    }

    internal void ApplyMutationNoLock(ApprovalMutation? mutation)
    {
        if (mutation is not null)
        {
            _records[mutation.Record.Id] = mutation.Record;
        }
    }

    private static string ScopeKey(string identity, string capability,
        string environment, string resource, string? operationId) =>
        string.Join('\u001f', identity, capability, environment, resource,
            operationId is null ? "legacy-context" : $"operation:{operationId}");

    private static string? NormalizeOperationId(string? operationId) =>
        string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();

    private static IReadOnlyCollection<ApprovalRecord> Order(
        IEnumerable<ApprovalRecord> records) => records
            .OrderByDescending(record => record.RequestedAt)
            .ThenBy(record => record.Id, StringComparer.Ordinal)
            .ToList();
}
