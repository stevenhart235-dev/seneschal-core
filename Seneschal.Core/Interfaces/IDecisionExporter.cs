using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IDecisionExporter
{
    Task ExportAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
