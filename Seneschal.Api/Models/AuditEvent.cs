namespace Seneschal.Api.Models;

public class AuditEvent
{
    public string Id { get; set; } = "";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string IdentityId { get; set; } = "";
    public string CapabilityId { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Decision { get; set; } = "";
    public string EnforcementMode { get; set; } = "";
    public List<string> MatchedPolicies { get; set; } = new();
    public List<string> Obligations { get; set; } = new();
    public string Reason { get; set; } = "";
    public long EvaluationDurationMs { get; set; }
}
