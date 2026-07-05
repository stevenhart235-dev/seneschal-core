namespace Seneschal.Core.Models;

public sealed record Resource
{
    public required string Type { get; init; }
    public required string Id { get; init; }
    public string? Environment { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
}