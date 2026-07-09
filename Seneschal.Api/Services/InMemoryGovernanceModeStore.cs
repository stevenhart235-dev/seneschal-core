using Seneschal.Core.Enums;

namespace Seneschal.Api.Services;

public sealed class InMemoryGovernanceModeStore : IGovernanceModeStore
{
    private readonly RuntimeSettings _runtimeSettings;
    private readonly object _sync = new();

    public InMemoryGovernanceModeStore(RuntimeSettings runtimeSettings)
    {
        _runtimeSettings = runtimeSettings;
    }

    public EnforcementMode GetMode()
    {
        lock (_sync)
        {
            return _runtimeSettings.Mode;
        }
    }

    public void SetMode(EnforcementMode mode)
    {
        lock (_sync)
        {
            _runtimeSettings.Mode = mode;
        }

        // TODO: Mode changes should become administrative audit events before
        // runtime governance controls are used in production.
    }
}
