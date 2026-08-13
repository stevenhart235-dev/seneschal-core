using System.Text.Json;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryAuditEventStore : IAuditEventStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new();

    private readonly List<StoredEvidence> _events = new();
    private readonly Dictionary<string, StoredEvidence> _eventsById =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private readonly Func<AuditEvent, Exception?>? _appendFailure;
    private readonly DateTimeOffset? _completeSinceUtc;

    public InMemoryAuditEventStore(
        Func<AuditEvent, Exception?>? appendFailure = null,
        DateTimeOffset? completeSinceUtc = null)
    {
        _appendFailure = appendFailure;
        _completeSinceUtc = completeSinceUtc;
    }

    internal object SyncRoot => _gate;

    public Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditEvent.Id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var pendingAppend = PrepareAppendNoLock(auditEvent);
            ApplyAppendNoLock(pendingAppend);
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
                    .OrderByDescending(item => item.TimestampUtc)
                    .Take(count)
                    .Select(Deserialize)
                    .ToList());
        }
    }

    public Task<AuditEvidenceCoverageBoundary> GetCoverageBoundaryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_completeSinceUtc.HasValue
            ? new AuditEvidenceCoverageBoundary
            {
                CompleteSinceUtc = _completeSinceUtc,
                Reason = "The in-memory evidence store has retained all writes since initialization."
            }
            : AuditEvidenceCoverageBoundary.Unknown);
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
                _eventsById.TryGetValue(id, out var stored)
                    ? Deserialize(stored)
                    : null);
        }
    }

    internal PendingEvidenceAppend? PrepareAppendNoLock(AuditEvent auditEvent)
    {
        var injectedFailure = _appendFailure?.Invoke(auditEvent);
        if (injectedFailure is not null)
        {
            throw injectedFailure;
        }

        var json = JsonSerializer.Serialize(auditEvent, SerializerOptions);

        if (_eventsById.TryGetValue(auditEvent.Id, out var existing))
        {
            if (string.Equals(existing.Json, json, StringComparison.Ordinal))
            {
                return null;
            }

            throw new EvaluationEvidenceConflictException(auditEvent.Id);
        }

        return new PendingEvidenceAppend(
            auditEvent.Id,
            auditEvent.TimestampUtc,
            json);
    }

    internal void ApplyAppendNoLock(PendingEvidenceAppend? pendingAppend)
    {
        if (pendingAppend is null)
        {
            return;
        }

        var stored = new StoredEvidence(
            pendingAppend.Id,
            pendingAppend.TimestampUtc,
            pendingAppend.Json);
        _events.Add(stored);
        _eventsById.Add(stored.Id, stored);
    }

    private static AuditEvent Deserialize(StoredEvidence stored)
    {
        return JsonSerializer.Deserialize<AuditEvent>(
            stored.Json,
            SerializerOptions)!;
    }

    internal sealed record PendingEvidenceAppend(
        string Id,
        DateTimeOffset TimestampUtc,
        string Json);

    private sealed record StoredEvidence(
        string Id,
        DateTimeOffset TimestampUtc,
        string Json);
}
