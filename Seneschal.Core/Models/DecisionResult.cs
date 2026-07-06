using Seneschal.Core.Enums;

namespace Seneschal.Core.Models;

public sealed record DecisionResult
{
    public required string DecisionId { get; init; }
    public required string RequestId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    public required DecisionType Decision { get; init; }
    public required EnforcementMode Mode { get; init; }
    public required string Reason { get; init; }

    public List<string> MatchedPolicies { get; init; } = new();
    public List<string> Obligations { get; init; } = new();

    public List<EvaluationStep> Evaluation { get; init; } = new();
    public int LatencyMs { get; init; }
}