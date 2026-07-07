namespace Seneschal.Core.Models;

public sealed record GraphData
{
    public IReadOnlyCollection<GraphNode> Nodes { get; init; } = [];
    public IReadOnlyCollection<GraphEdge> Edges { get; init; } = [];
}
