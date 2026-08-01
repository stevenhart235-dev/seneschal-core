using Microsoft.EntityFrameworkCore;
using Seneschal.Core.Enums;
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
        var existing = Find(identityId, capabilityId, environment, resourceId,
            operationId);
        if (existing is not null)
        {
            return new ApprovalLookupResult(existing, false);
        }

        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            IdentityId = identityId,
            CapabilityId = capabilityId,
            Environment = environment,
            ResourceId = resourceId,
            OperationId = PostgreSqlMappings.NormalizeOperationId(operationId),
            CorrelationMode = string.IsNullOrWhiteSpace(operationId)
                ? ApprovalCorrelationMode.LegacyContext
                : ApprovalCorrelationMode.Operation,
            RequestReason = requestReason,
            RequestedAt = requestedAt
        };
        using var context = contextFactory.CreateDbContext();
        context.Approvals.Add(PostgreSqlMappings.ToEntity(record));
        context.SaveChanges();
        return new ApprovalLookupResult(record, true);
    }

    public ApprovalRecord? Find(
        string identityId, string capabilityId, string environment,
        string resourceId, string? operationId = null)
    {
        operationId = PostgreSqlMappings.NormalizeOperationId(operationId);
        using var context = contextFactory.CreateDbContext();
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
        var entity = context.Approvals.SingleOrDefault(item =>
            item.Id == approvalId && item.Status == (int)ApprovalStatus.Pending);
        if (entity is null)
        {
            return null;
        }
        entity.Status = (int)status;
        entity.ResolvedAt = resolvedAt;
        entity.ResolvedBy = resolvedBy.Trim();
        context.SaveChanges();
        return PostgreSqlMappings.ToModel(entity);
    }

    public ApprovalRecord? Consume(
        string approvalId, string decisionId, DateTimeOffset consumedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        using var context = contextFactory.CreateDbContext();
        var entity = context.Approvals.SingleOrDefault(item =>
            item.Id == approvalId && item.Status == (int)ApprovalStatus.Approved);
        if (entity is null)
        {
            return null;
        }
        entity.Status = (int)ApprovalStatus.Consumed;
        entity.ConsumedAt = consumedAt;
        entity.ConsumedByDecisionId = decisionId;
        context.SaveChanges();
        return PostgreSqlMappings.ToModel(entity);
    }

    public IReadOnlyCollection<ApprovalRecord> GetAll()
    {
        using var context = contextFactory.CreateDbContext();
        return context.Approvals.AsNoTracking()
            .OrderBy(item => item.RequestedAt)
            .Select(item => PostgreSqlMappings.ToModel(item))
            .ToList();
    }
}
