using System.Security.Cryptography;
using System.Text;

namespace Seneschal.Core.Models;

public static class GovernanceIncidentKey
{
    public static string Create(
        string capabilityId,
        string identityId,
        string decisionReason,
        string matchedPolicy)
    {
        ArgumentNullException.ThrowIfNull(capabilityId);
        ArgumentNullException.ThrowIfNull(identityId);
        ArgumentNullException.ThrowIfNull(decisionReason);
        ArgumentNullException.ThrowIfNull(matchedPolicy);

        var values = new[]
        {
            Normalize(capabilityId),
            Normalize(identityId),
            Normalize(decisionReason),
            Normalize(matchedPolicy)
        };
        var canonical = string.Concat(values.Select(value =>
            $"{value.Length}:{value}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));

        return $"incident-{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant();
}
