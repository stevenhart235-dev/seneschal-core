using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryAuditEventStore : IAuditEventStore
{
    private readonly List<AuditEvent> _events = new();
    private readonly object _gate = new();

    public Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _events.Add(auditEvent);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyCollection<AuditEvent>>(
                _events
                    .OrderByDescending(auditEvent => auditEvent.TimestampUtc)
                    .Take(count)
                    .ToList());
        }
    }

    public Task<AuditEvent?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(
                _events.FirstOrDefault(auditEvent =>
                    string.Equals(
                        auditEvent.Id,
                        id,
                        StringComparison.OrdinalIgnoreCase)));
        }
    }
}
