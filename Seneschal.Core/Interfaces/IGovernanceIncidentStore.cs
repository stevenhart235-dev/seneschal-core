using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IGovernanceIncidentStore
{
    Task RecordAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GovernanceIncident>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<GovernanceIncident?> GetByIdAsync(
        string incidentId,
        CancellationToken cancellationToken = default);

    Task<bool> AcknowledgeAsync(
        string incidentId,
        CancellationToken cancellationToken = default);

    Task<bool> ResolveAsync(
        string incidentId,
        CancellationToken cancellationToken = default);
}
