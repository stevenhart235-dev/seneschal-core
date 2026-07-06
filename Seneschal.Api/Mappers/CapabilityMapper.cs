using ApiCapability = Seneschal.Api.Models.Capability;
using CoreCapability = Seneschal.Core.Models.Capability;
using Seneschal.Core.Enums;

namespace Seneschal.Api.Mappers;

public static class CapabilityMapper
{
    public static CoreCapability ToCore(ApiCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!Enum.TryParse<RiskLevel>(
                capability.Risk,
                ignoreCase: true,
                out var riskLevel))
        {
            throw new InvalidOperationException(
                $"Capability '{capability.Name}' has invalid risk level " +
                $"'{capability.Risk}'.");
        }

        return new CoreCapability
        {
            Id = capability.Name,
            Name = capability.Name,
            Provider = "api",
            Category = capability.Category,
            Description = capability.Description,
            RiskLevel = riskLevel
        };
    }
}
