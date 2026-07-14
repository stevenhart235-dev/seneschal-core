namespace Seneschal.Core.Models;

public sealed class PolicyEvaluation
{
    public required Policy Policy { get; init; }
    public bool Matched { get; init; }
    public List<string> Reasons { get; init; } = [];
    public List<string> Obligations { get; init; } = [];
    public List<string> RequiredApprovals { get; init; } = [];
    public List<EvaluationStep> Conditions { get; init; } = new();
}
