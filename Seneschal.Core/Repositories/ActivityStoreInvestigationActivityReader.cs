using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class ActivityStoreInvestigationActivityReader(
    IActivityStore activityStore,
    IAuditEventStore auditEventStore) : IInvestigationActivityReader
{
    public Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default) =>
        activityStore.GetSnapshotAsync(cancellationToken);

    public async Task<CapabilityInvestigationActivity?> GetCapabilityAsync(
        string capabilityId, int recentCount = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        if (recentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(recentCount));
        var snapshot = await activityStore.GetSnapshotAsync(cancellationToken);
        var activity = snapshot.Capabilities.FirstOrDefault(item =>
            string.Equals(item.CapabilityId, capabilityId,
                StringComparison.OrdinalIgnoreCase));
        if (activity is null) return null;
        var evidence = await EvaluationEvidenceAsync(recentCount,
            item => string.Equals(item.CapabilityId, capabilityId,
                StringComparison.OrdinalIgnoreCase), cancellationToken);
        return new CapabilityInvestigationActivity(
            activity,
            evidence.Select(item => item.IdentityId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToList(),
            evidence.Select(item => item.Environment)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToList(),
            evidence.Take(recentCount).ToList());
    }

    public async Task<IdentityInvestigationActivity?> GetIdentityAsync(
        string identityId, int recentCount = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);
        if (recentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(recentCount));
        var snapshot = await activityStore.GetSnapshotAsync(cancellationToken);
        var activity = snapshot.Identities.FirstOrDefault(item =>
            string.Equals(item.IdentityId, identityId,
                StringComparison.OrdinalIgnoreCase));
        if (activity is null) return null;
        var evidence = await EvaluationEvidenceAsync(recentCount,
            item => string.Equals(item.IdentityId, identityId,
                StringComparison.OrdinalIgnoreCase), cancellationToken);
        return new IdentityInvestigationActivity(
            activity,
            evidence.Select(item => item.Environment)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToList(),
            evidence.Take(recentCount).ToList());
    }

    private async Task<IReadOnlyCollection<AuditEvent>> EvaluationEvidenceAsync(
        int recentCount, Func<AuditEvent, bool> predicate,
        CancellationToken cancellationToken)
    {
        if (recentCount <= 0) return [];
        return (await auditEventStore.GetRecentAsync(
                int.MaxValue, cancellationToken))
            .Where(predicate)
            .ToList();
    }
}
