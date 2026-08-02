using Microsoft.EntityFrameworkCore;
using Npgsql;
using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlGovernanceIncidentStore(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory,
    ICapabilityCatalog capabilityCatalog) : IGovernanceIncidentStore
{
    public Task RecordAsync(AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<GovernanceIncident>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var projected = await ProjectAsync(cancellationToken);
        var ids = projected.Select(item => item.Id).ToList();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var states = await context.IncidentOperatorStates.AsNoTracking()
            .Where(item => ids.Contains(item.IncidentId))
            .ToDictionaryAsync(item => item.IncidentId, StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        return projected.Select(incident => states.TryGetValue(incident.Id, out var state)
                ? incident with
                {
                    CurrentStatus = (GovernanceIncidentStatus)state.Status,
                    OperatorStateVersion = state.Version
                }
                : incident)
            .ToList();
    }

    public async Task<GovernanceIncident?> GetByIdAsync(string incidentId,
        CancellationToken cancellationToken = default) =>
        (await GetAllAsync(cancellationToken)).SingleOrDefault(item =>
            string.Equals(item.Id, incidentId, StringComparison.OrdinalIgnoreCase));

    public async Task<GovernanceIncidentOperatorState?> GetOperatorStateAsync(
        string incidentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var state = await context.IncidentOperatorStates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IncidentId == incidentId,
                cancellationToken);
        return state is null ? null : ToModel(state);
    }

    public async Task<bool> AcknowledgeAsync(string incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await GetByIdAsync(incidentId, cancellationToken);
        return incident is not null && await AcknowledgeAsync(incidentId,
            incident.OperatorStateVersion, cancellationToken) is not null;
    }

    public async Task<bool> ResolveAsync(string incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await GetByIdAsync(incidentId, cancellationToken);
        return incident is not null && await ResolveAsync(incidentId,
            incident.OperatorStateVersion, cancellationToken) is not null;
    }

    public Task<GovernanceIncidentOperatorState?> AcknowledgeAsync(
        string incidentId, long expectedVersion,
        CancellationToken cancellationToken = default) => TransitionAsync(
            incidentId, expectedVersion, GovernanceIncidentStatus.Acknowledged,
            cancellationToken);

    public Task<GovernanceIncidentOperatorState?> ResolveAsync(
        string incidentId, long expectedVersion,
        CancellationToken cancellationToken = default) => TransitionAsync(
            incidentId, expectedVersion, GovernanceIncidentStatus.Resolved,
            cancellationToken);

    private async Task<GovernanceIncidentOperatorState?> TransitionAsync(
        string incidentId, long expectedVersion,
        GovernanceIncidentStatus resultingStatus,
        CancellationToken cancellationToken)
    {
        var incident = await GetByIdAsync(incidentId, cancellationToken);
        if (incident is null) return null;
        if (incident.OperatorStateVersion != expectedVersion)
            throw new OperationalControlConcurrencyException("incident operator",
                expectedVersion, incident.OperatorStateVersion);
        if (!IsValid(incident.CurrentStatus, resultingStatus)) return null;

        var timestamp = DateTimeOffset.UtcNow;
        var resultingVersion = expectedVersion + 1;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        if (expectedVersion == 0)
        {
            context.IncidentOperatorStates.Add(new IncidentOperatorStateEntity
            {
                IncidentId = incident.Id,
                Status = (int)resultingStatus,
                Version = resultingVersion,
                UpdatedAt = timestamp
            });
        }
        else
        {
            var updated = await context.IncidentOperatorStates
                .Where(item => item.IncidentId == incident.Id &&
                    item.Version == expectedVersion &&
                    item.Status == (int)incident.CurrentStatus)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, (int)resultingStatus)
                    .SetProperty(item => item.Version, resultingVersion)
                    .SetProperty(item => item.UpdatedAt, timestamp), cancellationToken);
            if (updated != 1)
                throw new OperationalControlConcurrencyException("incident operator",
                    expectedVersion, (await GetOperatorStateAsync(incident.Id,
                        cancellationToken))?.Version ?? 0);
        }

        var evidence = CreateEvidence(incident, resultingStatus,
            resultingVersion, timestamp);
        await PostgreSqlGovernanceModeStore.AddEvidenceAsync(context, evidence,
            cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new OperationalControlConcurrencyException("incident operator",
                expectedVersion, (await GetOperatorStateAsync(incident.Id,
                    cancellationToken))?.Version ?? expectedVersion);
        }
        return new GovernanceIncidentOperatorState
        {
            IncidentId = incident.Id,
            Status = resultingStatus,
            Version = resultingVersion
        };
    }

    private async Task<IReadOnlyCollection<GovernanceIncident>> ProjectAsync(
        CancellationToken cancellationToken)
    {
        var capabilities = await capabilityCatalog.SearchAsync(
            new CapabilityCatalogQuery(), cancellationToken);
        var projector = new InMemoryGovernanceIncidentStore(
            capabilities.Select(item => item.Capability));
        var evidence = await new PostgreSqlAuditEventStore(contextFactory)
            .GetRecentAsync(int.MaxValue, cancellationToken);
        foreach (var auditEvent in evidence.OrderBy(item => item.TimestampUtc))
        {
            if (IsAdministrativeEvidence(auditEvent.EffectiveAction))
                continue;
            await projector.RecordAsync(auditEvent, cancellationToken);
        }
        return await projector.GetAllAsync(cancellationToken);
    }

    private static bool IsAdministrativeEvidence(string effectiveAction) =>
        effectiveAction.StartsWith("approval_", StringComparison.Ordinal) ||
        effectiveAction.StartsWith("runtime_mode_", StringComparison.Ordinal) ||
        effectiveAction.StartsWith("governance_window_", StringComparison.Ordinal) ||
        effectiveAction.StartsWith("incident_", StringComparison.Ordinal);

    private static bool IsValid(GovernanceIncidentStatus current,
        GovernanceIncidentStatus resulting) =>
        resulting == GovernanceIncidentStatus.Acknowledged
            ? current == GovernanceIncidentStatus.Open
            : resulting == GovernanceIncidentStatus.Resolved &&
              current is GovernanceIncidentStatus.Open or GovernanceIncidentStatus.Acknowledged;

    private static GovernanceIncidentOperatorState ToModel(
        IncidentOperatorStateEntity entity) => new()
    {
        IncidentId = entity.IncidentId,
        Status = (GovernanceIncidentStatus)entity.Status,
        Version = entity.Version
    };

    private static AuditEvent CreateEvidence(GovernanceIncident incident,
        GovernanceIncidentStatus status, long version, DateTimeOffset timestamp) => new()
    {
        Id = Guid.NewGuid().ToString("N"), RequestId = incident.Id,
        TimestampUtc = timestamp, IdentityId = "operator identity unavailable",
        CapabilityId = incident.CapabilityId, Decision = DecisionType.Allow,
        PolicyDecision = DecisionType.Allow, EnforcementMode = EnforcementMode.LogOnly,
        EffectiveAction = status == GovernanceIncidentStatus.Acknowledged
            ? "incident_acknowledged" : "incident_resolved",
        Reason = $"Incident {status.ToString().ToLowerInvariant()}.",
        MatchedPolicies = string.IsNullOrWhiteSpace(incident.MatchedPolicy)
            ? [] : [incident.MatchedPolicy],
        RequestContext = new()
        {
            ["incidentId"] = incident.Id,
            ["previousStatus"] = incident.CurrentStatus.ToString(),
            ["resultingStatus"] = status.ToString(),
            ["previousVersion"] = incident.OperatorStateVersion.ToString(),
            ["resultingVersion"] = version.ToString(),
            ["decisionReason"] = incident.DecisionReason,
            ["occurrenceCount"] = incident.OccurrenceCount.ToString(),
            ["firstObserved"] = incident.FirstSeenUtc.ToString("O"),
            ["lastObserved"] = incident.LastSeenUtc.ToString("O")
        }
    };
}
