namespace Seneschal.Api.Models;

public class DecisionResult
{
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public string PolicyMatched { get; set; } = "";
    public long DurationMs { get; set; }
    public string EffectiveAction { get; set; } = "";
    public string Mode { get; set; } = "";
    public string ExecutionGuidance { get; set; } = "";
    public string? ApprovalId { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? OperationId { get; set; }
    public string? ApprovalCorrelationMode { get; set; }
    public string? Message { get; set; }
    public string? RetryGuidance { get; set; }
}
