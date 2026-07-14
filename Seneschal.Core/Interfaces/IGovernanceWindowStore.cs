using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Core.Interfaces;

public interface IGovernanceWindowStore
{
    GovernanceWindow GetWindow();
    void SetState(bool enabled, GovernanceWindowMode mode);
}
