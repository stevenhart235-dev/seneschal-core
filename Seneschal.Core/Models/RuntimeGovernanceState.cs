using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record RuntimeGovernanceState
{
    public required EnforcementMode Mode { get; init; }
    public long Version { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public string? Reason { get; init; }
}
