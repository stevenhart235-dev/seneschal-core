namespace Seneschal.Core.Models;

public sealed record CapabilityActivity
{
    public required string CapabilityId { get; init; }
    public long TotalRequests { get; init; }
    public long AllowedCount { get; init; }
    public long DeniedCount { get; init; }
    public long PendingApprovalCount { get; init; }
    public DateTimeOffset? LastUsedUtc { get; init; }
    public double AverageEvaluationDurationMs { get; init; }
}
