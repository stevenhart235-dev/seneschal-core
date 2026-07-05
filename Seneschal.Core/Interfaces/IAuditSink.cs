using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IAuditSink
{
    Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}