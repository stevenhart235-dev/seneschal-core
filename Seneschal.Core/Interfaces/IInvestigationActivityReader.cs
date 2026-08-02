using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IInvestigationActivityReader
{
    Task<ActivitySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
    Task<CapabilityInvestigationActivity?> GetCapabilityAsync(
        string capabilityId,
        int recentCount = 100,
        CancellationToken cancellationToken = default);
    Task<IdentityInvestigationActivity?> GetIdentityAsync(
        string identityId,
        int recentCount = 100,
        CancellationToken cancellationToken = default);
}

public sealed record CapabilityInvestigationActivity(
    CapabilityActivity Activity,
    IReadOnlyCollection<string> ObservedIdentities,
    IReadOnlyCollection<string> Environments,
    IReadOnlyCollection<AuditEvent> RecentEvidence);

public sealed record IdentityInvestigationActivity(
    IdentityActivity Activity,
    IReadOnlyCollection<string> Environments,
    IReadOnlyCollection<AuditEvent> RecentEvidence);
