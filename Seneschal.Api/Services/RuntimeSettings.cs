using Seneschal.Core.Enums;

namespace Seneschal.Api.Services;

public class RuntimeSettings
{
    public EnforcementMode Mode { get; set; } = EnforcementMode.LogOnly;
}
