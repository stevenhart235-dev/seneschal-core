using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface ICapabilityCatalog
{
    Task<CapabilityCatalogEntry?> GetByIdAsync(
        string capabilityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CapabilityCatalogEntry>> SearchAsync(
        CapabilityCatalogQuery query,
        CancellationToken cancellationToken = default);
}
