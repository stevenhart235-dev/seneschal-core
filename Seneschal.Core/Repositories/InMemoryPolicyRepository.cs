using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryPolicyRepository : IPolicyRepository
{
    private readonly IReadOnlyCollection<Policy> _policies;

    public InMemoryPolicyRepository(IEnumerable<Policy> policies)
    {
        _policies = policies.ToList();
    }

    public Task<IReadOnlyCollection<Policy>> GetPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_policies);
    }
}