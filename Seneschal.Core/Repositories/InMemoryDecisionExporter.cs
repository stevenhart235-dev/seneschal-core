using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryDecisionExporter : IDecisionExporter
{
    private readonly List<DecisionExportRecord> _records = new();
    private readonly object _gate = new();

    public Task ExportAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        var record = new DecisionExportRecord
        {
            Timestamp = auditEvent.TimestampUtc,
            Identity = auditEvent.IdentityId,
            Capability = auditEvent.CapabilityId,
            Environment = auditEvent.Environment,
            Decision = auditEvent.Decision.ToString(),
            MatchedPolicy = auditEvent.MatchedPolicies.FirstOrDefault(
                policy => !string.IsNullOrWhiteSpace(policy)) ?? "n/a",
            EvaluationDurationMs = auditEvent.EvaluationDurationMs,
            Reason = auditEvent.Reason
        };

        lock (_gate)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DecisionExportRecord>> GetExportsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<DecisionExportRecord>>(
                _records
                    .OrderByDescending(record => record.Timestamp)
                    .ToList());
        }
    }
}
