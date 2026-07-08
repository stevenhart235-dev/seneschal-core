namespace Seneschal.Core.Models;

public sealed record DecisionExportRecord
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Identity { get; init; }
    public required string Capability { get; init; }
    public string Environment { get; init; } = string.Empty;
    public required string Decision { get; init; }
    public string MatchedPolicy { get; init; } = "n/a";
    public int EvaluationDurationMs { get; init; }
    public required string Reason { get; init; }
}
