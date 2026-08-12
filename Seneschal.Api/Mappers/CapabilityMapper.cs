using ApiCapability = Seneschal.Api.Models.Capability;
using CoreCapability = Seneschal.Core.Models.Capability;
using Seneschal.Core.Enums;

namespace Seneschal.Api.Mappers;

public static class CapabilityMapper
{
    public static CoreCapability ToCore(ApiCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!TryParseRiskLevel(capability.Risk, out var riskLevel))
        {
            throw new InvalidOperationException(
                $"Capability '{capability.Name}' has invalid risk level " +
                $"'{capability.Risk}'.");
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
            Technology = capability.Technology,
            Tags = capability.Tags?.ToList() ?? []
        };
    }

    public static bool TryParseRiskLevel(string? configuredRisk, out RiskLevel riskLevel)
    {
        var value = string.IsNullOrWhiteSpace(configuredRisk)
            ? nameof(RiskLevel.Low)
            : configuredRisk;
        return Enum.TryParse(value, ignoreCase: true, out riskLevel) &&
            Enum.IsDefined(riskLevel);
    }
}
