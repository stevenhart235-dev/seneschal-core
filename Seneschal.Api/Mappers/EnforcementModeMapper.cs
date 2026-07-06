using CoreEnforcementMode = Seneschal.Core.Enums.EnforcementMode;

namespace Seneschal.Api.Mappers;

public static class EnforcementModeMapper
{
    public static string ToApi(CoreEnforcementMode mode)
    {
        return mode switch
        {
            CoreEnforcementMode.LogOnly => "LogOnly",
            CoreEnforcementMode.Enforce => "Enforce",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported Core enforcement mode.")
        };
    }
}
