using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IAuditEventStore : IAuditSink
{
    /// <summary>
    /// Appends immutable evaluation evidence. Repeating identical evidence by
    /// ID succeeds without adding another record. Different content under an
    /// existing ID throws EvaluationEvidenceConflictException. Provider and
    /// cancellation failures propagate to the caller.
    /// </summary>
    new Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    Task<AuditEvent?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns evidence by descending timestamp. Evidence with equal
    /// timestamps retains append order.
    /// </summary>
    Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
        int count = 100,
        CancellationToken cancellationToken = default);
}
