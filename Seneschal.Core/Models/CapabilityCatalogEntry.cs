namespace Seneschal.Core.Models;

public sealed record CapabilityCatalogEntry
{
    public required Capability Capability { get; init; }
}
