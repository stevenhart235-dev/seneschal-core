using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryGovernanceIncidentStore : IGovernanceIncidentStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IncidentAccumulator> _incidents =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, RiskLevel> _capabilityRiskLevels;

    public InMemoryGovernanceIncidentStore()
        : this(Array.Empty<Capability>())
    {
    }

    public InMemoryGovernanceIncidentStore(
        IEnumerable<Capability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        _capabilityRiskLevels = capabilities
            .GroupBy(capability => capability.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().RiskLevel,
                StringComparer.OrdinalIgnoreCase);
    }

    public Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ShouldRecord(auditEvent))
        {
            return Task.CompletedTask;
        }

        var matchedPolicy = auditEvent.MatchedPolicies
            .FirstOrDefault(policy => !string.IsNullOrWhiteSpace(policy)) ??
            string.Empty;
        var key = BuildKey(
            auditEvent.CapabilityId,
            auditEvent.IdentityId,
            auditEvent.Reason,
            matchedPolicy);

        lock (_gate)
        {
            if (!_incidents.TryGetValue(key, out var incident))
            {
                incident = new IncidentAccumulator(
                    auditEvent.CapabilityId,
                    auditEvent.IdentityId,
                    auditEvent.Reason,
                    matchedPolicy,
                    auditEvent.TimestampUtc);
                _incidents[key] = incident;
            }

            incident.Record(auditEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<GovernanceIncident>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<GovernanceIncident>>(
                _incidents.Values
                    .Select(incident => incident.ToIncident(
                        GetRiskLevel(incident.CapabilityId)))
                    .OrderBy(incident => incident.Severity)
                    .ThenByDescending(incident => incident.LastSeenUtc)
                    .ThenByDescending(incident => incident.OccurrenceCount)
                    .ThenBy(incident => incident.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        }
    }

    public Task<GovernanceIncident?> GetByIdAsync(
        string incidentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var incident = FindById(incidentId);

            return Task.FromResult(
                incident?.ToIncident(GetRiskLevel(incident.CapabilityId)));
        }
    }

    public Task<bool> AcknowledgeAsync(
        string incidentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var incident = FindById(incidentId);

            return Task.FromResult(incident?.Acknowledge() == true);
        }
    }

    public Task<bool> ResolveAsync(
        string incidentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(incidentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var incident = FindById(incidentId);

            return Task.FromResult(incident?.Resolve() == true);
        }
    }

    private RiskLevel? GetRiskLevel(string capabilityId)
    {
        return _capabilityRiskLevels.TryGetValue(capabilityId, out var riskLevel)
            ? riskLevel
            : null;
    }

    private IncidentAccumulator? FindById(string incidentId)
    {
        return _incidents.Values.FirstOrDefault(incident => string.Equals(
            incident.Id,
            incidentId,
            StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldRecord(AuditEvent auditEvent)
    {
        return auditEvent.Decision is
            DecisionType.Deny or
            DecisionType.RequireApproval or
            DecisionType.Warn or
            DecisionType.LogOnly;
    }

    private static string BuildKey(
        string capabilityId,
        string identityId,
        string reason,
        string matchedPolicy)
    {
        return string.Join(
            "|",
            capabilityId.Trim(),
            identityId.Trim(),
            reason.Trim(),
            matchedPolicy.Trim());
    }

    private sealed class IncidentAccumulator
    {
        private readonly HashSet<DecisionType> _decisions = new();

        public IncidentAccumulator(
            string capabilityId,
            string identityId,
            string decisionReason,
            string matchedPolicy,
            DateTimeOffset firstSeenUtc)
        {
            Id = Guid.NewGuid().ToString("N");
            CapabilityId = capabilityId;
            IdentityId = identityId;
            DecisionReason = decisionReason;
            MatchedPolicy = matchedPolicy;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = firstSeenUtc;
        }

        public string Id { get; }

        public string CapabilityId { get; }

        public string IdentityId { get; }

        public string DecisionReason { get; }

        public string MatchedPolicy { get; }

        public DateTimeOffset FirstSeenUtc { get; }

        public DateTimeOffset LastSeenUtc { get; private set; }

        public int OccurrenceCount { get; private set; }

        public GovernanceIncidentStatus CurrentStatus { get; private set; } =
            GovernanceIncidentStatus.Open;

        public void Record(AuditEvent auditEvent)
        {
            OccurrenceCount++;
            LastSeenUtc = auditEvent.TimestampUtc > LastSeenUtc
                ? auditEvent.TimestampUtc
                : LastSeenUtc;
            _decisions.Add(auditEvent.Decision);
        }

        public GovernanceIncident ToIncident(RiskLevel? capabilityRiskLevel)
        {
            var severity = DetermineSeverity(capabilityRiskLevel);

            return new GovernanceIncident
            {
                Id = Id,
                Title = BuildTitle(severity),
                Severity = severity,
                CapabilityId = CapabilityId,
                IdentityId = IdentityId,
                DecisionReason = DecisionReason,
                MatchedPolicy = MatchedPolicy,
                FirstSeenUtc = FirstSeenUtc,
                LastSeenUtc = LastSeenUtc,
                OccurrenceCount = OccurrenceCount,
                CurrentStatus = CurrentStatus
            };
        }

        public bool Acknowledge()
        {
            if (CurrentStatus != GovernanceIncidentStatus.Open)
            {
                return false;
            }

            CurrentStatus = GovernanceIncidentStatus.Acknowledged;

            return true;
        }

        public bool Resolve()
        {
            if (CurrentStatus == GovernanceIncidentStatus.Resolved)
            {
                return false;
            }

            CurrentStatus = GovernanceIncidentStatus.Resolved;

            return true;
        }

        private GovernanceIncidentSeverity DetermineSeverity(
            RiskLevel? capabilityRiskLevel)
        {
            var isRepeated = OccurrenceCount > 1;

            if (isRepeated &&
                _decisions.Contains(DecisionType.Deny) &&
                capabilityRiskLevel == RiskLevel.Critical)
            {
                return GovernanceIncidentSeverity.Critical;
            }

            if (isRepeated &&
                (_decisions.Contains(DecisionType.Deny) ||
                 _decisions.Contains(DecisionType.RequireApproval)))
            {
                return GovernanceIncidentSeverity.Warning;
            }

            return GovernanceIncidentSeverity.Info;
        }

        private string BuildTitle(GovernanceIncidentSeverity severity)
        {
            return severity switch
            {
                GovernanceIncidentSeverity.Critical =>
                    $"Repeated denied governance decision for critical capability '{CapabilityId}'",
                GovernanceIncidentSeverity.Warning =>
                    $"Repeated governance decision for capability '{CapabilityId}'",
                _ =>
                    $"Governance observation for capability '{CapabilityId}'"
            };
        }
    }
}
