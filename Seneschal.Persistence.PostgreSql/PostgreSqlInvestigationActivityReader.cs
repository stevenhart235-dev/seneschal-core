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
            .Where(item => !item.EffectiveAction.StartsWith("approval_"));

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
}
