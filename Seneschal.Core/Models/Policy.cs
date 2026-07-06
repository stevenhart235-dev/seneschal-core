using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record Policy
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DecisionType Effect { get; init; }
    public required string Reason { get; init; }

    public Dictionary<string, string> Conditions { get; init; } = new();
    public List<string> Obligations { get; init; } = new();
    public int Priority { get; init; } = 100;
}