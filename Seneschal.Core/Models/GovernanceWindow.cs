using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record GovernanceWindow
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required bool Enabled { get; init; }
    public required GovernanceWindowMode Mode { get; init; }
    public IReadOnlyCollection<string> AffectedCapabilities { get; init; } = [];
    public required string Reason { get; init; }
    public long Version { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}
