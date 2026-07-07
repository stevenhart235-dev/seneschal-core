using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IActivityStore
{
    Task RecordAsync(
        AuditEvent decisionEvent,
        CancellationToken cancellationToken = default);

    Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}
