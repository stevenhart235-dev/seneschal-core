using Microsoft.EntityFrameworkCore;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlInvestigationActivityReader(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory,
    IActivityStore transientActivityStore) : IInvestigationActivityReader
{
    public async Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var evaluations = context.EvaluationEvidence.AsNoTracking()
            .Where(item => !item.EffectiveAction.StartsWith("approval_") &&
                !item.EffectiveAction.StartsWith("runtime_mode_") &&
                !item.EffectiveAction.StartsWith("governance_window_"));

        var capabilities = await evaluations
            .GroupBy(item => item.CapabilityId)
            .Select(group => new CapabilityActivity
            {
                CapabilityId = group.Key,
                TotalRequests = group.LongCount(),
                AllowedCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.Allow)),
                DeniedCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.Deny)),
                PendingApprovalCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.RequireApproval)),
                LastUsedUtc = group.Max(item => item.TimestampUtc)
            })
            .OrderBy(item => item.CapabilityId)
            .ToListAsync(cancellationToken);

        var identityTotals = await evaluations
            .GroupBy(item => item.IdentityId)
            .Select(group => new IdentityActivity
            {
                IdentityId = group.Key,
                TotalRequests = group.LongCount(),
                DeniedCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.Deny)),
                PendingApprovalCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.RequireApproval)),
                LastUsedUtc = group.Max(item => item.TimestampUtc)
            })
            .OrderBy(item => item.IdentityId)
            .ToListAsync(cancellationToken);
        var identityCapabilities = await evaluations
            .Select(item => new { item.IdentityId, item.CapabilityId })
            .Distinct()
            .ToListAsync(cancellationToken);
        var capabilitiesByIdentity = identityCapabilities
            .GroupBy(item => item.IdentityId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .Select(item => item.CapabilityId)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
        var identities = identityTotals
            .Select(identity => identity with
            {
                DistinctCapabilitiesUsed = capabilitiesByIdentity
                    .GetValueOrDefault(identity.IdentityId, [])
            })
            .ToList();

        // Matched policies and duration are not extracted relational columns.
        // Keep those projections process-local rather than querying JSONB for
        // aggregate values or reporting invented durable metrics.
        var transient = await transientActivityStore.GetSnapshotAsync(
            cancellationToken);
        return new ActivitySnapshot
        {
            Capabilities = capabilities,
            Identities = identities,
            Policies = transient.Policies
        };
    }

    public async Task<CapabilityInvestigationActivity?> GetCapabilityAsync(
        string capabilityId, int recentCount = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);
        if (recentCount < 0) throw new ArgumentOutOfRangeException(nameof(recentCount));
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var evaluations = Evaluations(context).Where(item =>
            item.CapabilityId == capabilityId);
        var activity = await evaluations.GroupBy(item => item.CapabilityId)
            .Select(group => new CapabilityActivity
            {
                CapabilityId = group.Key,
                TotalRequests = group.LongCount(),
                AllowedCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.Allow)),
                DeniedCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.Deny)),
                PendingApprovalCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.RequireApproval)),
                LastUsedUtc = group.Max(item => item.TimestampUtc)
            }).SingleOrDefaultAsync(cancellationToken);
        if (activity is null) return null;
        var identities = await evaluations.Select(item => item.IdentityId)
            .Distinct().OrderBy(item => item).ToListAsync(cancellationToken);
        var environments = await evaluations.Select(item => item.Environment)
            .Distinct().OrderBy(item => item).ToListAsync(cancellationToken);
        return new CapabilityInvestigationActivity(activity, identities,
            environments, await RecentEvidenceAsync(
                context.EvaluationEvidence.AsNoTracking().Where(item =>
                    item.CapabilityId == capabilityId),
                recentCount, cancellationToken));
    }

    public async Task<IdentityInvestigationActivity?> GetIdentityAsync(
        string identityId, int recentCount = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);
        if (recentCount < 0) throw new ArgumentOutOfRangeException(nameof(recentCount));
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var evaluations = Evaluations(context).Where(item =>
            item.IdentityId == identityId);
        var activity = await evaluations.GroupBy(item => item.IdentityId)
            .Select(group => new IdentityActivity
            {
                IdentityId = group.Key,
                TotalRequests = group.LongCount(),
                DeniedCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.Deny)),
                PendingApprovalCount = group.LongCount(item =>
                    item.Decision == nameof(DecisionType.RequireApproval)),
                LastUsedUtc = group.Max(item => item.TimestampUtc)
            }).SingleOrDefaultAsync(cancellationToken);
        if (activity is null) return null;
        var capabilities = await evaluations.Select(item => item.CapabilityId)
            .Distinct().OrderBy(item => item).ToListAsync(cancellationToken);
        var environments = await evaluations.Select(item => item.Environment)
            .Distinct().OrderBy(item => item).ToListAsync(cancellationToken);
        return new IdentityInvestigationActivity(
            activity with { DistinctCapabilitiesUsed = capabilities },
            environments,
            await RecentEvidenceAsync(
                context.EvaluationEvidence.AsNoTracking().Where(item =>
                    item.IdentityId == identityId),
                recentCount, cancellationToken));
    }

    private static IQueryable<EvaluationEvidenceEntity> Evaluations(
        PostgreSqlPersistenceDbContext context) =>
        context.EvaluationEvidence.AsNoTracking()
            .Where(item => !item.EffectiveAction.StartsWith("approval_") &&
                !item.EffectiveAction.StartsWith("runtime_mode_") &&
                !item.EffectiveAction.StartsWith("governance_window_"));

    private static async Task<IReadOnlyCollection<AuditEvent>> RecentEvidenceAsync(
        IQueryable<EvaluationEvidenceEntity> evaluations, int recentCount,
        CancellationToken cancellationToken)
    {
        if (recentCount == 0) return [];
        var payloads = await evaluations
            .OrderByDescending(item => item.TimestampUtc)
            .ThenBy(item => item.AppendSequence)
            .Take(recentCount)
            .Select(item => item.Payload)
            .ToListAsync(cancellationToken);
        return payloads.Select(AuditEventSerialization.Deserialize).ToList();
    }
}
