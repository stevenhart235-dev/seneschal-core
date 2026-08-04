using Microsoft.EntityFrameworkCore;
using Npgsql;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlAuditEventStore(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory) :
    IAuditEventStore
{
    public async Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditEvent.Id);
        cancellationToken.ThrowIfCancellationRequested();

        var (payload, hash) = AuditEventSerialization.Serialize(auditEvent);
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var existing = await context.EvaluationEvidence
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == auditEvent.Id,
                cancellationToken);

        if (existing is not null)
        {
            EnsureIdentical(existing, auditEvent.Id, hash);
            return;
        }

        context.EvaluationEvidence.Add(ToEntity(auditEvent, payload, hash));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            context.ChangeTracker.Clear();
            existing = await context.EvaluationEvidence
                .AsNoTracking()
                .SingleAsync(item => item.Id == auditEvent.Id,
                    cancellationToken);
            EnsureIdentical(existing, auditEvent.Id, hash);
        }
    }

    public async Task<AuditEvent?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();
        return await contextFactory.ExecuteAsync(async (context, token) =>
        {
            var payload = await context.EvaluationEvidence
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.Payload)
                .SingleOrDefaultAsync(token);
            return payload is null
                ? null : AuditEventSerialization.Deserialize(payload);
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await contextFactory.ExecuteAsync(async (context, token) =>
        {
            var payloads = await context.EvaluationEvidence
                .AsNoTracking()
                .OrderByDescending(item => item.TimestampUtc)
                .ThenBy(item => item.AppendSequence)
                .Take(count)
                .Select(item => item.Payload)
                .ToListAsync(token);
            return (IReadOnlyCollection<AuditEvent>)payloads
                .Select(AuditEventSerialization.Deserialize).ToList();
        }, cancellationToken);
    }

    internal static EvaluationEvidenceEntity ToEntity(
        AuditEvent evidence, string payload, string hash) => new()
    {
        Id = evidence.Id,
        TimestampUtc = evidence.TimestampUtc,
        IdentityId = evidence.IdentityId,
        CapabilityId = evidence.CapabilityId,
        Environment = evidence.Environment,
        ResourceId = evidence.ResourceId,
        Decision = evidence.Decision.ToString(),
        EffectiveAction = evidence.EffectiveAction,
        ApprovalId = evidence.ApprovalId,
        OperationId = evidence.ApprovalOperationId,
        ContentHash = hash,
        Payload = payload
    };

    internal static void EnsureIdentical(
        EvaluationEvidenceEntity existing, string evidenceId, string hash)
    {
        if (!string.Equals(existing.ContentHash, hash, StringComparison.Ordinal))
        {
            throw new EvaluationEvidenceConflictException(evidenceId);
        }
    }

    internal static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
