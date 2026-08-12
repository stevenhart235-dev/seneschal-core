namespace Seneschal.Api.Models;

public sealed class CapabilityPackFile
{
    public CapabilityPackMetadata Pack { get; set; } = new();
    public List<Capability> Capabilities { get; set; } = [];
}

public sealed class CapabilityPackMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
}
