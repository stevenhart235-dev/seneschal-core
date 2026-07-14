using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Core.Repositories;

public sealed class InMemoryGovernanceWindowStore : IGovernanceWindowStore
{
    public const string ProductionFreezeName = "Production Freeze";
    public const string ProductionFreezeReason = "Weekend production freeze.";

    private readonly object _gate = new();
    private bool _enabled;
    private GovernanceWindowMode _mode = GovernanceWindowMode.Observe;

    public GovernanceWindow GetWindow()
    {
        lock (_gate)
        {
            return new GovernanceWindow
            {
                Name = ProductionFreezeName,
                Description = "Manually control high-risk production changes during a production freeze.",
                Enabled = _enabled,
                Mode = _mode,
                AffectedCapabilities =
                [
                    "production.deployment.execute",
                    "infrastructure.production.apply",
                    "infrastructure.production.destroy"
                ],
                Reason = ProductionFreezeReason
            };
        }
    }

    public void SetState(bool enabled, GovernanceWindowMode mode)
    {
        lock (_gate)
        {
            _enabled = enabled;
            _mode = mode;
        }
    }
}
