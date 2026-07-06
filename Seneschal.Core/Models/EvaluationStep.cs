namespace Seneschal.Core.Models;

public sealed record EvaluationStep
{
    public required string Property { get; init; }

    public required string Expected { get; init; }

    public required string Actual { get; init; }

    public required bool Matched { get; init; }
}