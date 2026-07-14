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
        string resourceId, string requestReason, DateTimeOffset requestedAt)
    {
        var key = ScopeKey(identityId, capabilityId, environment, resourceId);
        lock (_gate)
        {
            if (_records.TryGetValue(key, out var existing))
                return new ApprovalLookupResult(existing, false);

            var record = new ApprovalRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                IdentityId = identityId,
                CapabilityId = capabilityId,
                Environment = environment,
                ResourceId = resourceId,
                RequestReason = requestReason,
                RequestedAt = requestedAt
            };
            _records[key] = record;
            return new ApprovalLookupResult(record, true);
        }
    }

    public ApprovalRecord? Find(
        string identityId, string capabilityId, string environment,
        string resourceId)
    {
        lock (_gate)
        {
            return _records.GetValueOrDefault(
                ScopeKey(identityId, capabilityId, environment, resourceId));
        }
    }

    public ApprovalRecord? Resolve(
        string approvalId, ApprovalStatus status, string resolvedBy,
        DateTimeOffset resolvedAt)
    {
        if (status == ApprovalStatus.Pending || string.IsNullOrWhiteSpace(resolvedBy))
            return null;
        lock (_gate)
        {
            var pair = _records.FirstOrDefault(item =>
                item.Value.Id.Equals(approvalId, StringComparison.OrdinalIgnoreCase));
            if (pair.Value is null || pair.Value.Status != ApprovalStatus.Pending)
                return null;
            var resolved = pair.Value with
            {
                Status = status,
                ResolvedAt = resolvedAt,
                ResolvedBy = resolvedBy.Trim()
            };
            _records[pair.Key] = resolved;
            return resolved;
        }
    }

    public IReadOnlyCollection<ApprovalRecord> GetAll()
    {
        lock (_gate) return _records.Values.ToList();
    }

    private static string ScopeKey(string identity, string capability,
        string environment, string resource) =>
        string.Join('\u001f', identity, capability, environment, resource);
}
