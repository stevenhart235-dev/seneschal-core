namespace Seneschal.Core.Models;

public sealed record PolicyActivity
{
    public required string PolicyId { get; init; }
    public long MatchCount { get; init; }
    public DateTimeOffset? LastMatchedUtc { get; init; }
}
