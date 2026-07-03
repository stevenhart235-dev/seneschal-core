namespace Seneschal.Api.Models;

public class AuditEvent
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Identity { get; set; } = "";
    public string Capability { get; set; } = "";
    public Dictionary<string, string> Context { get; set; } = new();
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public string PolicyMatched { get; set; } = "";
    public long DurationMs { get; set; }
}