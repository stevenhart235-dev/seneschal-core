namespace Seneschal.Api.Models;

public sealed record LoadedCapability
{
    public required Capability Capability { get; init; }
    public required IReadOnlyList<CapabilitySource> Sources { get; init; }
}

public sealed record CapabilitySource
{
    public required string Kind { get; init; }
    public string PackId { get; init; } = string.Empty;
    public string PackVersion { get; init; } = string.Empty;
    public required string Path { get; init; }
}
