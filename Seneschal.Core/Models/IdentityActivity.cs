namespace Seneschal.Core.Models;

public sealed record IdentityActivity
{
    public required string IdentityId { get; init; }
    public long TotalRequests { get; init; }
    public IReadOnlyCollection<string> DistinctCapabilitiesUsed { get; init; }
        = [];
    public long DeniedCount { get; init; }
    public long PendingApprovalCount { get; init; }
    public DateTimeOffset? LastUsedUtc { get; init; }
}
