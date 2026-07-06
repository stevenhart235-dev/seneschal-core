using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IGovernanceGraph
{
    Task<IReadOnlyCollection<GovernanceRelationship>> QueryAsync(
        GovernanceRelationshipQuery query,
        CancellationToken cancellationToken = default);
}
