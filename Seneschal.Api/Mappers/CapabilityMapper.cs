using ApiCapability = Seneschal.Api.Models.Capability;
using CoreCapability = Seneschal.Core.Models.Capability;
using Seneschal.Core.Enums;

namespace Seneschal.Api.Mappers;

public static class CapabilityMapper
{
    public static CoreCapability ToCore(ApiCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var configuredRisk = string.IsNullOrWhiteSpace(capability.Risk)
            ? nameof(RiskLevel.Low)
            : capability.Risk;

        if (!Enum.TryParse<RiskLevel>(
                configuredRisk,
                ignoreCase: true,
                out var riskLevel))
        {
            throw new InvalidOperationException(
                $"Capability '{capability.Name}' has invalid risk level " +
                $"'{configuredRisk}'.");
        }

        return new CoreCapability
        {
            Id = capability.Name,
            Name = capability.Name,
            DisplayName = string.IsNullOrWhiteSpace(capability.DisplayName)
                ? capability.Name
                : capability.DisplayName,
            Provider = "api",
            Category = string.IsNullOrWhiteSpace(capability.Category)
                ? "Uncategorized"
                : capability.Category,
            Description = capability.Description,
            RiskLevel = riskLevel,
            Owner = capability.Owner,
            Lifecycle = string.IsNullOrWhiteSpace(capability.Lifecycle)
                ? "Active"
                : capability.Lifecycle,
            DocumentationUrl = capability.DocumentationUrl,
            Tags = capability.Tags?.ToList() ?? []
        };
    }
}
