using Microsoft.EntityFrameworkCore;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlPersistenceDbContext(
    DbContextOptions<PostgreSqlPersistenceDbContext> options) : DbContext(options)
{
    internal DbSet<EvaluationEvidenceEntity> EvaluationEvidence =>
        Set<EvaluationEvidenceEntity>();

    internal DbSet<ApprovalEntity> Approvals => Set<ApprovalEntity>();
    internal DbSet<EvidenceCoverageMetadataEntity> EvidenceCoverageMetadata => Set<EvidenceCoverageMetadataEntity>();
    internal DbSet<RuntimeGovernanceStateEntity> RuntimeGovernanceStates => Set<RuntimeGovernanceStateEntity>();
    internal DbSet<GovernanceWindowStateEntity> GovernanceWindowStates => Set<GovernanceWindowStateEntity>();
    internal DbSet<IncidentOperatorStateEntity> IncidentOperatorStates => Set<IncidentOperatorStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        ConfigureModel(modelBuilder);

    internal static void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EvaluationEvidenceEntity>(entity =>
        {
            entity.ToTable("evaluation_evidence");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.AppendSequence)
                .HasColumnName("append_sequence")
                .UseIdentityAlwaysColumn();
            entity.HasIndex(item => item.AppendSequence).IsUnique();
            entity.Property(item => item.TimestampUtc).HasColumnName("timestamp_utc");
            entity.Property(item => item.IdentityId).HasColumnName("identity_id").HasMaxLength(256);
            entity.Property(item => item.CapabilityId).HasColumnName("capability_id").HasMaxLength(256);
            entity.Property(item => item.Environment).HasColumnName("environment").HasMaxLength(128);
            entity.Property(item => item.ResourceId).HasColumnName("resource_id").HasMaxLength(512);
            entity.Property(item => item.Decision).HasColumnName("decision").HasMaxLength(64);
            entity.Property(item => item.EffectiveAction).HasColumnName("effective_action").HasMaxLength(64);
            entity.Property(item => item.ApprovalId).HasColumnName("approval_id").HasMaxLength(128);
            entity.Property(item => item.OperationId).HasColumnName("operation_id").HasMaxLength(256);
            entity.Property(item => item.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
            entity.Property(item => item.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.HasIndex(item => new { item.TimestampUtc, item.AppendSequence })
                .IsDescending(true, false);
            entity.HasIndex(item => item.IdentityId);
            entity.HasIndex(item => item.CapabilityId);
        });

        modelBuilder.Entity<EvidenceCoverageMetadataEntity>(entity =>
        {
            entity.ToTable("evidence_coverage_metadata");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.CompleteSinceUtc).HasColumnName("complete_since_utc");
        });
        modelBuilder.Entity<ApprovalEntity>(entity =>
        {
            entity.ToTable("approvals", table => table.HasCheckConstraint(
                "CK_approvals_status", "status >= 0 AND status <= 3"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id").HasMaxLength(128);
            entity.Property(item => item.IdentityId).HasColumnName("identity_id").HasMaxLength(256);
            entity.Property(item => item.CapabilityId).HasColumnName("capability_id").HasMaxLength(256);
            entity.Property(item => item.Environment).HasColumnName("environment").HasMaxLength(128);
            entity.Property(item => item.ResourceId).HasColumnName("resource_id").HasMaxLength(512);
            entity.Property(item => item.OperationId).HasColumnName("operation_id").HasMaxLength(256);
            entity.Property(item => item.CorrelationMode).HasColumnName("correlation_mode");
            entity.Property(item => item.RequestReason).HasColumnName("request_reason");
            entity.Property(item => item.RequestedAt).HasColumnName("requested_at");
            entity.Property(item => item.Status).HasColumnName("status");
            entity.Property(item => item.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(item => item.ResolvedBy).HasColumnName("resolved_by").HasMaxLength(256);
            entity.Property(item => item.ConsumedAt).HasColumnName("consumed_at");
            entity.Property(item => item.ConsumedByDecisionId).HasColumnName("consumed_by_decision_id").HasMaxLength(128);
            entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            entity.HasIndex(item => new
            {
                item.IdentityId,
                item.CapabilityId,
                item.Environment,
                item.ResourceId,
                item.OperationId
            });
            entity.HasIndex(item => new { item.Status, item.RequestedAt, item.Id })
                .IsDescending(false, true, false);
        });

        modelBuilder.Entity<RuntimeGovernanceStateEntity>(entity =>
        {
            entity.ToTable("runtime_governance_state", table => table.HasCheckConstraint(
                "CK_runtime_governance_state_mode", "mode >= 0 AND mode <= 1"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Mode).HasColumnName("mode");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(256);
            entity.Property(item => item.Reason).HasColumnName("reason");
            entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.Entity<GovernanceWindowStateEntity>(entity =>
        {
            entity.ToTable("governance_window_state", table => table.HasCheckConstraint(
                "CK_governance_window_state_mode", "mode >= 0 AND mode <= 1"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Enabled).HasColumnName("enabled");
            entity.Property(item => item.Mode).HasColumnName("mode");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.UpdatedBy).HasColumnName("updated_by").HasMaxLength(256);
            entity.Property(item => item.Reason).HasColumnName("reason");
            entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
        });

        modelBuilder.Entity<IncidentOperatorStateEntity>(entity =>
        {
            entity.ToTable("incident_operator_state", table => table.HasCheckConstraint(
                "CK_incident_operator_state_status", "status >= 0 AND status <= 2"));
            entity.HasKey(item => item.IncidentId);
            entity.Property(item => item.IncidentId).HasColumnName("incident_id").HasMaxLength(73);
            entity.Property(item => item.Status).HasColumnName("status");
            entity.Property(item => item.Version).HasColumnName("version").IsConcurrencyToken();
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });
    }
}

internal sealed class IncidentOperatorStateEntity
{
    public required string IncidentId { get; set; }
    public int Status { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class RuntimeGovernanceStateEntity
{
    public short Id { get; set; }
    public int Mode { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? Reason { get; set; }
    public long Version { get; set; }
}

internal sealed class GovernanceWindowStateEntity
{
    public short Id { get; set; }
    public bool Enabled { get; set; }
    public int Mode { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? Reason { get; set; }
    public long Version { get; set; }
}

internal sealed class EvaluationEvidenceEntity
{
    public required string Id { get; set; }
    public long AppendSequence { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public required string IdentityId { get; set; }
    public required string CapabilityId { get; set; }
    public required string Environment { get; set; }
    public required string ResourceId { get; set; }
    public required string Decision { get; set; }
    public required string EffectiveAction { get; set; }
    public string? ApprovalId { get; set; }
    public string? OperationId { get; set; }
    public required string ContentHash { get; set; }
    public required string Payload { get; set; }
}

internal sealed class EvidenceCoverageMetadataEntity
{
    public short Id { get; set; }
    public DateTimeOffset CompleteSinceUtc { get; set; }
}
internal sealed class ApprovalEntity
{
    public required string Id { get; set; }
    public required string IdentityId { get; set; }
    public required string CapabilityId { get; set; }
    public required string Environment { get; set; }
    public required string ResourceId { get; set; }
    public string? OperationId { get; set; }
    public int CorrelationMode { get; set; }
    public required string RequestReason { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string? ConsumedByDecisionId { get; set; }
    public long Version { get; set; }
}
