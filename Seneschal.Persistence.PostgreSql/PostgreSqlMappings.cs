using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql;

internal static class PostgreSqlMappings
{
    public static ApprovalEntity ToEntity(ApprovalRecord record) => new()
    {
        Id = record.Id,
        IdentityId = record.IdentityId,
        CapabilityId = record.CapabilityId,
        Environment = record.Environment,
        ResourceId = record.ResourceId,
        OperationId = NormalizeOperationId(record.OperationId),
        CorrelationMode = (int)record.CorrelationMode,
        RequestReason = record.RequestReason,
        RequestedAt = record.RequestedAt,
        Status = (int)record.Status,
        ResolvedAt = record.ResolvedAt,
        ResolvedBy = record.ResolvedBy,
        ConsumedAt = record.ConsumedAt,
        ConsumedByDecisionId = record.ConsumedByDecisionId
    };

    public static ApprovalRecord ToModel(ApprovalEntity entity) => new()
    {
        Id = entity.Id,
        IdentityId = entity.IdentityId,
        CapabilityId = entity.CapabilityId,
        Environment = entity.Environment,
        ResourceId = entity.ResourceId,
        OperationId = entity.OperationId,
        CorrelationMode = (ApprovalCorrelationMode)entity.CorrelationMode,
        RequestReason = entity.RequestReason,
        RequestedAt = entity.RequestedAt,
        Status = (ApprovalStatus)entity.Status,
        ResolvedAt = entity.ResolvedAt,
        ResolvedBy = entity.ResolvedBy,
        ConsumedAt = entity.ConsumedAt,
        ConsumedByDecisionId = entity.ConsumedByDecisionId
    };

    public static void Apply(ApprovalEntity target, ApprovalRecord source)
    {
        var mapped = ToEntity(source);
        target.Status = mapped.Status;
        target.ResolvedAt = mapped.ResolvedAt;
        target.ResolvedBy = mapped.ResolvedBy;
        target.ConsumedAt = mapped.ConsumedAt;
        target.ConsumedByDecisionId = mapped.ConsumedByDecisionId;
    }

    public static string? NormalizeOperationId(string? operationId) =>
        string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
}
