namespace Seneschal.Api.Models;

public sealed class IntegrationApiKey
{
    public string Name { get; set; } = "";

    public string Key { get; set; } = "";

    public bool Enabled { get; set; }

    public List<string> AllowedIdentities { get; set; } = new();

    public List<string> AllowedCapabilities { get; set; } = new();

    public string? Environment { get; set; }
}
