using Seneschal.Core.Enums;
namespace Seneschal.Core.Models;


public sealed record PolicyMatch
{
    public required string PolicyId { get; init; }
    public required string PolicyName { get; init; }
    public required int Priority { get; init; }
    public required DecisionType Effect { get; init; }
    public required string Reason { get; init; }

    public List<string> Obligations { get; init; } = new();

    public List<EvaluationStep> Evaluation { get; init; } = new();
}