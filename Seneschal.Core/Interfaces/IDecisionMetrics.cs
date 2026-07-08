using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IDecisionMetrics
{
    Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
