namespace Seneschal.Core.Models;

public sealed record GraphEdge
{
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public required string RelationshipType { get; init; }
    public string? Label { get; init; }
}
