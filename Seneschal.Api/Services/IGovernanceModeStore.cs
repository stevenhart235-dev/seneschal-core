using Seneschal.Core.Enums;

namespace Seneschal.Api.Services;

public interface IGovernanceModeStore
{
    EnforcementMode GetMode();

    void SetMode(EnforcementMode mode);
}
