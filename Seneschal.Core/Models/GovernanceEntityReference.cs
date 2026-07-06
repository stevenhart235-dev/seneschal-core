using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record GovernanceEntityReference
{
    public required GovernanceEntityType Type { get; init; }
    public required string Id { get; init; }
    public string? Scope { get; init; }
}
