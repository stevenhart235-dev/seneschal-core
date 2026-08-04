using Microsoft.EntityFrameworkCore;
using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlApprovalStore(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory) :
    IApprovalStore
{
    public ApprovalLookupResult GetOrCreate(
        string identityId, string capabilityId, string environment,
        string resourceId, string requestReason, DateTimeOffset requestedAt,
        string? operationId = null)
    {
        operationId = PostgreSqlMappings.NormalizeOperationId(operationId);
        var scope = string.Join('\u001f', identityId, capabilityId, environment,
            resourceId, operationId ?? "legacy-context");
        using var context = contextFactory.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();
        context.Database.ExecuteSqlInterpolated(
            $"SELECT pg_advisory_xact_lock(hashtextextended({scope}, 0));");
        var existingEntity = context.Approvals.AsNoTracking()
            .Where(item => item.IdentityId == identityId &&
                item.CapabilityId == capabilityId &&
                item.Environment == environment &&
                item.ResourceId == resourceId &&
                item.OperationId == operationId &&
                item.Status != (int)ApprovalStatus.Consumed)
            .OrderByDescending(item => item.RequestedAt)
            .FirstOrDefault();
        var existing = existingEntity is null
            ? null : PostgreSqlMappings.ToModel(existingEntity);
        if (existing is not null)
        {
            transaction.Commit();
            return new ApprovalLookupResult(existing, false);
        }

        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            IdentityId = identityId,
            CapabilityId = capabilityId,
            Environment = environment,
            ResourceId = resourceId,
            OperationId = operationId,
            CorrelationMode = string.IsNullOrWhiteSpace(operationId)
                ? ApprovalCorrelationMode.LegacyContext
                : ApprovalCorrelationMode.Operation,
            RequestReason = requestReason,
            RequestedAt = requestedAt
        };
        context.Approvals.Add(PostgreSqlMappings.ToEntity(record));
        context.SaveChanges();
        transaction.Commit();
        return new ApprovalLookupResult(record, true);
    }

    public ApprovalRecord? Find(
        string identityId, string capabilityId, string environment,
        string resourceId, string? operationId = null)
    {
        operationId = PostgreSqlMappings.NormalizeOperationId(operationId);
        return contextFactory.Execute(context =>
        {
            var entity = context.Approvals.AsNoTracking()
                .Where(item => item.IdentityId == identityId &&
                    item.CapabilityId == capabilityId &&
                    item.Environment == environment &&
                    item.ResourceId == resourceId &&
                    item.OperationId == operationId &&
                    item.Status != (int)ApprovalStatus.Consumed)
                .OrderByDescending(item => item.RequestedAt)
                .FirstOrDefault();
            return entity is null ? null : PostgreSqlMappings.ToModel(entity);
        });
    }

    public ApprovalRecord? GetById(string approvalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        return contextFactory.Execute(context =>
        {
            var entity = context.Approvals.AsNoTracking()
                .SingleOrDefault(item => item.Id == approvalId);
            return entity is null ? null : PostgreSqlMappings.ToModel(entity);
        });
    }

    public IReadOnlyCollection<ApprovalRecord> GetPending()
    {
        return contextFactory.Execute(context => Ordered(
            context.Approvals.AsNoTracking().Where(item =>
                item.Status == (int)ApprovalStatus.Pending)));
    }

    public IReadOnlyCollection<ApprovalRecord> GetHistory()
    {
        return contextFactory.Execute(context => Ordered(
            context.Approvals.AsNoTracking().Where(item =>
                item.Status != (int)ApprovalStatus.Pending)));
    }

    public ApprovalRecord? Resolve(
        string approvalId, ApprovalStatus status, string resolvedBy,
        DateTimeOffset resolvedAt)
    {
        if (status is not (ApprovalStatus.Approved or ApprovalStatus.Rejected) ||
            string.IsNullOrWhiteSpace(resolvedBy))
        {
            return null;
        }

        using var context = contextFactory.CreateDbContext();
        var entity = context.Approvals.SingleOrDefault(item => item.Id == approvalId);
        if (entity is null)
        {
            return null;
        }
        if (entity.Status != (int)ApprovalStatus.Pending)
        {
            throw new ApprovalTransitionException(approvalId,
                (ApprovalStatus)entity.Status, status);
        }
        entity.Status = (int)status;
        entity.ResolvedAt = resolvedAt;
        entity.ResolvedBy = resolvedBy.Trim();
        entity.Version++;
        try
        {
            context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw CurrentTransition(approvalId, status);
        }
        return PostgreSqlMappings.ToModel(entity);
    }

    public ApprovalRecord? Consume(
        string approvalId, string decisionId, DateTimeOffset consumedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        using var context = contextFactory.CreateDbContext();
        var entity = context.Approvals.SingleOrDefault(item => item.Id == approvalId);
        if (entity is null)
        {
            return null;
        }
        if (entity.Status != (int)ApprovalStatus.Approved)
        {
            throw new ApprovalTransitionException(approvalId,
                (ApprovalStatus)entity.Status, ApprovalStatus.Consumed);
        }
        entity.Status = (int)ApprovalStatus.Consumed;
        entity.ConsumedAt = consumedAt;
        entity.ConsumedByDecisionId = decisionId;
        entity.Version++;
        try
        {
            context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw CurrentTransition(approvalId, ApprovalStatus.Consumed);
        }
        return PostgreSqlMappings.ToModel(entity);
    }

    public IReadOnlyCollection<ApprovalRecord> GetAll()
    {
        return contextFactory.Execute(context =>
            Ordered(context.Approvals.AsNoTracking()));
    }

    private static IReadOnlyCollection<ApprovalRecord> Ordered(
        IQueryable<ApprovalEntity> query) => query
            .OrderByDescending(item => item.RequestedAt)
            .ThenBy(item => item.Id)
            .AsEnumerable()
            .Select(PostgreSqlMappings.ToModel)
            .ToList();

    private ApprovalTransitionException CurrentTransition(
        string approvalId, ApprovalStatus requestedStatus)
    {
        using var context = contextFactory.CreateDbContext();
        var status = context.Approvals.AsNoTracking()
            .Where(item => item.Id == approvalId)
            .Select(item => (ApprovalStatus)item.Status)
            .SingleOrDefault();
        return new ApprovalTransitionException(
            approvalId, status, requestedStatus);
    }
}
