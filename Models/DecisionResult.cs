namespace Seneschal.Api.Models;

public class DecisionResult
{
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public string PolicyMatched { get; set; } = "";
    public long DurationMs { get; set; }
    public string EffectiveAction { get; set; } = "";
    public string Mode { get; set; } = "";
}