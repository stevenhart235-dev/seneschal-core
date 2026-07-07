namespace Seneschal.Core.Models;

public sealed record GraphNode
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Type { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
    public string? Group { get; init; }
}
