using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IGovernanceModeStore
{
    RuntimeGovernanceState GetState() => new() { Mode = GetMode() };

    EnforcementMode GetMode() => GetState().Mode;

    void SetMode(EnforcementMode mode) =>
        throw new NotSupportedException("This provider requires a versioned mutation.");

    Task<RuntimeGovernanceState> SetModeAsync(
        EnforcementMode mode,
        long expectedVersion,
        string? actor = null,
        string? reason = null,
        string? operationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetMode(mode);
        return Task.FromResult(GetState());
    }
}
