namespace Seneschal.Core.Models;

public sealed record AuditEvidenceCoverageBoundary
{
    public static AuditEvidenceCoverageBoundary Unknown { get; } = new();
    public DateTimeOffset? CompleteSinceUtc { get; init; }
    public string Reason { get; init; } =
        "The evidence source does not expose a provable retention boundary.";
}