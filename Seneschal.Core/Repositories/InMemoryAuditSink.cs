using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryAuditSink : IAuditSink
{
    public List<AuditEvent> Events { get; } = new();

    public Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}