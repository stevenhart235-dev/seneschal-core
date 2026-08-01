using Microsoft.EntityFrameworkCore;
using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlEvaluationCommitCoordinator(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory) :
    IEvaluationCommitCoordinator
{
    public async Task CommitAsync(
        EvaluationCommit evaluationCommit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluationCommit);
        ArgumentNullException.ThrowIfNull(evaluationCommit.Evidence);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(
                cancellationToken);
            await using var transaction = await context.Database
                .BeginTransactionAsync(cancellationToken);

            await PrepareApprovalAsync(context,
                evaluationCommit.ApprovalMutation, cancellationToken);
            await PrepareEvidenceAsync(context, evaluationCommit.Evidence,
                cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EvaluationCommitException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EvaluationCommitException(
                "Required evaluation effects could not be committed.", exception);
        }
    }

    private static async Task PrepareEvidenceAsync(
        PostgreSqlPersistenceDbContext context,
        AuditEvent evidence,
        CancellationToken cancellationToken)
    {
        var (payload, hash) = AuditEventSerialization.Serialize(evidence);
        var existing = await context.EvaluationEvidence.SingleOrDefaultAsync(
            item => item.Id == evidence.Id, cancellationToken);
        if (existing is not null)
        {
            PostgreSqlAuditEventStore.EnsureIdentical(existing, evidence.Id, hash);
            return;
        }
        context.EvaluationEvidence.Add(
            PostgreSqlAuditEventStore.ToEntity(evidence, payload, hash));
    }

    private static async Task PrepareApprovalAsync(
        PostgreSqlPersistenceDbContext context,
        ApprovalMutation? mutation,
        CancellationToken cancellationToken)
    {
        if (mutation is null)
        {
            return;
        }

        if (mutation.Kind == ApprovalMutationKind.Create)
        {
            var record = mutation.Record;
            var operationId = PostgreSqlMappings.NormalizeOperationId(record.OperationId);
            var scope = string.Join('\u001f', record.IdentityId,
                record.CapabilityId, record.Environment, record.ResourceId,
                operationId ?? "legacy-context");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0));",
                cancellationToken);
            var existing = await context.Approvals.SingleOrDefaultAsync(
                item => item.Id == record.Id, cancellationToken);
            if (existing is not null &&
                PostgreSqlMappings.ToModel(existing) == record)
            {
                return;
            }
            if (existing is not null)
            {
                throw new EvaluationCommitException(
                    $"Approval '{record.Id}' already exists with different content.");
            }
            var scopeExists = await context.Approvals.AnyAsync(item =>
                item.IdentityId == record.IdentityId &&
                item.CapabilityId == record.CapabilityId &&
                item.Environment == record.Environment &&
                item.ResourceId == record.ResourceId &&
                item.OperationId == operationId &&
                item.Status != (int)ApprovalStatus.Consumed,
                cancellationToken);
            if (scopeExists)
            {
                throw new EvaluationCommitException(
                    "Approval scope changed before the evaluation could commit.");
            }
            context.Approvals.Add(PostgreSqlMappings.ToEntity(record));
            return;
        }

        if (mutation.Kind == ApprovalMutationKind.Consume &&
            mutation.ExpectedStatus is not null &&
            mutation.Record.Status == ApprovalStatus.Consumed)
        {
            var target = mutation.Record;
            var updated = await context.Approvals
                .Where(item => item.Id == target.Id &&
                    item.Status == (int)mutation.ExpectedStatus.Value)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, (int)target.Status)
                    .SetProperty(item => item.ConsumedAt, target.ConsumedAt)
                    .SetProperty(item => item.ConsumedByDecisionId,
                        target.ConsumedByDecisionId), cancellationToken);
            if (updated == 1)
            {
                return;
            }
            var existing = await context.Approvals.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == target.Id,
                    cancellationToken);
            if (existing is not null &&
                PostgreSqlMappings.ToModel(existing) == target)
            {
                return;
            }
        }

        throw new EvaluationCommitException(
            $"Approval '{mutation.Record.Id}' could not be consumed atomically.");
    }
}
