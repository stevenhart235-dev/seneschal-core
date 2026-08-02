using Microsoft.EntityFrameworkCore;
using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlGovernanceModeStore(IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory) : IGovernanceModeStore
{
    public void SetMode(EnforcementMode mode) =>
        SetModeAsync(mode, GetState().Version).GetAwaiter().GetResult();
    public RuntimeGovernanceState GetState()
    {
        using var context = contextFactory.CreateDbContext();
        return Map(context.RuntimeGovernanceStates.AsNoTracking().Single(item => item.Id == 1));
    }

    public async Task<RuntimeGovernanceState> SetModeAsync(EnforcementMode mode, long expectedVersion,
        string? actor = null, string? reason = null, string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var entity = await context.RuntimeGovernanceStates.SingleAsync(item => item.Id == 1, cancellationToken);
        var previous = Map(entity);
        if (previous.Mode == mode) return previous;
        if (previous.Version != expectedVersion)
            throw new OperationalControlConcurrencyException("runtime governance mode", expectedVersion, previous.Version);
        entity.Mode = (int)mode; entity.Version++; entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = NormalizeActor(actor);
        entity.Reason = NormalizeReason(reason, "Runtime governance mode changed.");
        var updated = Map(entity);
        await AddEvidenceAsync(context, CreateEvidence(previous, updated, operationId), cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new OperationalControlConcurrencyException("runtime governance mode", expectedVersion, GetState().Version); }
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    private static RuntimeGovernanceState Map(RuntimeGovernanceStateEntity entity) => new()
    { Mode = (EnforcementMode)entity.Mode, Version = entity.Version, UpdatedAt = entity.UpdatedAt, UpdatedBy = entity.UpdatedBy, Reason = entity.Reason };
    internal static string NormalizeActor(string? value) => string.IsNullOrWhiteSpace(value) ? "operator identity unavailable" : value.Trim();
    internal static string NormalizeReason(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    internal static async Task AddEvidenceAsync(PostgreSqlPersistenceDbContext context, AuditEvent evidence, CancellationToken token)
    {
        var (payload, hash) = AuditEventSerialization.Serialize(evidence);
        var existing = await context.EvaluationEvidence.SingleOrDefaultAsync(item => item.Id == evidence.Id, token);
        if (existing is not null) { PostgreSqlAuditEventStore.EnsureIdentical(existing, evidence.Id, hash); return; }
        context.EvaluationEvidence.Add(PostgreSqlAuditEventStore.ToEntity(evidence, payload, hash));
    }

    private static AuditEvent CreateEvidence(RuntimeGovernanceState previous,
        RuntimeGovernanceState updated, string? operationId) => new()
    {
        Id = string.IsNullOrWhiteSpace(operationId) ? Guid.NewGuid().ToString("N") : $"runtime-mode-{operationId.Trim()}",
        RequestId = operationId?.Trim() ?? string.Empty,
        TimestampUtc = updated.UpdatedAt!.Value,
        IdentityId = updated.UpdatedBy!,
        CapabilityId = "seneschal.runtime-governance.manage",
        Decision = DecisionType.Allow, PolicyDecision = DecisionType.Allow,
        EnforcementMode = updated.Mode, EffectiveAction = "runtime_mode_changed",
        Reason = updated.Reason!,
        RequestContext = new()
        {
            ["previousMode"] = previous.Mode.ToString(), ["resultingMode"] = updated.Mode.ToString(),
            ["previousVersion"] = previous.Version.ToString(), ["resultingVersion"] = updated.Version.ToString()
        }
    };
}

public sealed class PostgreSqlGovernanceWindowStore(IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory) : IGovernanceWindowStore
{
    public void SetState(bool enabled, GovernanceWindowMode mode) =>
        SetStateAsync(enabled, mode, GetWindow().Version).GetAwaiter().GetResult();
    public GovernanceWindow GetWindow()
    {
        using var context = contextFactory.CreateDbContext();
        return Map(context.GovernanceWindowStates.AsNoTracking().Single(item => item.Id == 1));
    }

    public async Task<GovernanceWindow> SetStateAsync(bool enabled, GovernanceWindowMode mode, long expectedVersion,
        string? actor = null, string? reason = null, string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var entity = await context.GovernanceWindowStates.SingleAsync(item => item.Id == 1, cancellationToken);
        var previous = Map(entity);
        if (previous.Enabled == enabled && previous.Mode == mode) return previous;
        if (previous.Version != expectedVersion)
            throw new OperationalControlConcurrencyException("Governance Window", expectedVersion, previous.Version);
        entity.Enabled = enabled; entity.Mode = (int)mode; entity.Version++;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = PostgreSqlGovernanceModeStore.NormalizeActor(actor);
        entity.Reason = PostgreSqlGovernanceModeStore.NormalizeReason(reason, "Governance Window state changed.");
        var updated = Map(entity);
        await PostgreSqlGovernanceModeStore.AddEvidenceAsync(context,
            InMemoryGovernanceWindowStore.CreateEvidence(previous, updated, entity.Reason, operationId), cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new OperationalControlConcurrencyException("Governance Window", expectedVersion, GetWindow().Version); }
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    private static GovernanceWindow Map(GovernanceWindowStateEntity entity) => new()
    {
        Name = InMemoryGovernanceWindowStore.ProductionFreezeName,
        Description = "Manually control high-risk production changes during a production freeze.",
        Enabled = entity.Enabled, Mode = (GovernanceWindowMode)entity.Mode,
        AffectedCapabilities = ["production.deployment.execute", "infrastructure.production.apply", "infrastructure.production.destroy"],
        Reason = InMemoryGovernanceWindowStore.ProductionFreezeReason,
        Version = entity.Version, UpdatedAt = entity.UpdatedAt, UpdatedBy = entity.UpdatedBy
    };
}
