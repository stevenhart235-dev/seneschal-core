namespace Seneschal.Core.Models;

public sealed record DecisionRequest
{
    public required string RequestId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? OperationId { get; init; }

    public required Identity Identity { get; init; }
    public required Capability Capability { get; init; }
    public required Intent Intent { get; init; }
    public required Resource Resource { get; init; }

    public Dictionary<string, string> Context { get; init; } = new();
}
