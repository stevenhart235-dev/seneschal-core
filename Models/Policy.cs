namespace Seneschal.Api.Models;

public class Policy
{
    public string Name { get; set; } = "";
    public string Identity { get; set; } = "";
    public string Capability { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
}