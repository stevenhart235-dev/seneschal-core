using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record Identity
{
    public required string Id { get; init; }
    public required IdentityType Type { get; init; }
    public required string Owner { get; init; }
    public required string Environment { get; init; }
    public string? DisplayName { get; init; }
    public string? Application { get; init; }
    public string? Technology { get; init; }
    public string? Description { get; init; }
    public Dictionary<string, string> Attributes { get; init; } = new();
}
