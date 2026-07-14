namespace Seneschal.Api.Models;

public class Capability
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Risk { get; set; } = "";
    public string Category { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Lifecycle { get; set; } = "";
    public string DocumentationUrl { get; set; } = "";
    public List<string> Tags { get; set; } = new();
}
