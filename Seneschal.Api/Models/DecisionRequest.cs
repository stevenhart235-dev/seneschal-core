namespace Seneschal.Api.Models;

public class DecisionRequest
{
    public string Identity { get; set; } = "";
    public string Capability { get; set; } = "";
    public string? OperationId { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
}
