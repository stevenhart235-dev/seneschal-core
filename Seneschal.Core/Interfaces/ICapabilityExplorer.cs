using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface ICapabilityExplorer
{
    Task<CapabilityOverview?> GetOverviewAsync(
        CapabilityExplorerQuery query,
        CancellationToken cancellationToken = default);
}
