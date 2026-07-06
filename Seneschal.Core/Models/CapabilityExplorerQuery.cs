namespace Seneschal.Core.Models;

public sealed record CapabilityExplorerQuery
{
    public required string CapabilityId { get; init; }
    public DateTimeOffset? ActiveAt { get; init; }
}
