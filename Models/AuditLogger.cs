using System.Text.Json;
using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public class AuditLogger
{
    private readonly string _auditFilePath;

    public AuditLogger()
    {
        var auditDirectory = Path.Combine(AppContext.BaseDirectory, "Audit");
        Directory.CreateDirectory(auditDirectory);

        _auditFilePath = Path.Combine(auditDirectory, "audit.jsonl");
    }

    public void Log(DecisionRequest request, DecisionResult result)
    {
        var auditEvent = new AuditEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Identity = request.Identity,
            Capability = request.Capability,
            Context = request.Context,
            Decision = result.Decision,
            Reason = result.Reason,
            PolicyMatched = result.PolicyMatched,
            DurationMs = result.DurationMs
        };

        var json = JsonSerializer.Serialize(auditEvent);
        File.AppendAllText(_auditFilePath, json + Environment.NewLine);
    }
}