using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record Capability
{
    private RiskLevel _riskLevel;

    public required string Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public required string Provider { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public RiskLevel RiskLevel
    {
        get => _riskLevel;
        init => _riskLevel = value;
    }

    // Retained for compatibility with existing requests and policy evaluation.
    public RiskLevel Risk
    {
        get => _riskLevel;
        init => _riskLevel = value;
    }

    public string Owner { get; init; } = string.Empty;
    public string Lifecycle { get; init; } = string.Empty;
    public string DocumentationUrl { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
}
