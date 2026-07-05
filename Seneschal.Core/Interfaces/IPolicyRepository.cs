using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IPolicyRepository
{
    Task<IReadOnlyCollection<Policy>> GetPoliciesAsync(
        CancellationToken cancellationToken = default);
}