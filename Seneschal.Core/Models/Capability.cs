using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record Capability
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string Category { get; init; }
    public required RiskLevel Risk { get; init; }
    public required string Description { get; init; }
    public List<string> Tags { get; init; } = new();
}