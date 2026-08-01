using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql;

internal static class AuditEventSerialization
{
    private static readonly JsonSerializerOptions Options = new();

    public static (string Payload, string Hash) Serialize(AuditEvent evidence)
    {
        var canonical = evidence with
        {
            RequestContext = evidence.RequestContext
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Value,
                    StringComparer.Ordinal),
            PolicyEvaluations = evidence.PolicyEvaluations
                .Select(Canonicalize)
                .ToList()
        };
        var payload = JsonSerializer.Serialize(canonical, Options);
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return (payload, hash);
    }

    public static AuditEvent Deserialize(string payload) =>
        JsonSerializer.Deserialize<AuditEvent>(payload, Options)!;

    private static PolicyEvaluation Canonicalize(PolicyEvaluation evaluation) =>
        new()
        {
            Policy = evaluation.Policy with
            {
                Conditions = evaluation.Policy.Conditions
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .ToDictionary(item => item.Key, item => item.Value,
                        StringComparer.Ordinal)
            },
            Matched = evaluation.Matched,
            Reasons = evaluation.Reasons,
            Obligations = evaluation.Obligations,
            RequiredApprovals = evaluation.RequiredApprovals,
            Conditions = evaluation.Conditions
        };
}
