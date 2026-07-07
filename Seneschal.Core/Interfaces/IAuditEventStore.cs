using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IAuditEventStore : IAuditSink
{
    Task<AuditEvent?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
        int count = 100,
        CancellationToken cancellationToken = default);
}
