using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IGovernanceWindowStore
{
    GovernanceWindow GetWindow();
    void SetState(bool enabled, GovernanceWindowMode mode);

    Task<GovernanceWindow> SetStateAsync(
        bool enabled,
        GovernanceWindowMode mode,
        long expectedVersion,
        string? actor = null,
        string? reason = null,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(enabled, mode);
        return Task.FromResult(GetWindow());
    }
}
