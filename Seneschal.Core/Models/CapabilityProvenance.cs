namespace Seneschal.Core.Models;

public sealed record CapabilityProvenance
{
    public required string Kind { get; init; }
    public string PackId { get; init; } = string.Empty;
    public string PackVersion { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
}
