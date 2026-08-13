using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Seneschal.Core.Interfaces;

namespace Seneschal.Api.Services;

public sealed class GovernanceConfigurationFingerprintService
{
    private readonly PolicyLoader _policies;
    private readonly IGovernanceModeStore _mode;
    private readonly IGovernanceWindowStore? _window;

    public GovernanceConfigurationFingerprintService(
        PolicyLoader policies,
        IGovernanceModeStore mode,
        IGovernanceWindowStore? window = null)
    {
        _policies = policies;
        _mode = mode;
        _window = window;
    }

    public string GetCurrentFingerprint()
    {
        return Compute(_policies.GetCorePolicies(), _mode.GetMode(), _window?.GetWindow());
    }

    public static string Compute(IEnumerable<Seneschal.Core.Models.Policy> corePolicies,
        Seneschal.Core.Enums.EnforcementMode mode, Seneschal.Core.Models.GovernanceWindow? governanceWindow)
    {
        var policies = corePolicies
            .Select(policy => new
            {
                policy.Id,
                Effect = policy.Effect.ToString(),
                policy.Reason,
                policy.Priority,
                Conditions = policy.Conditions.OrderBy(item => item.Key,
                    StringComparer.Ordinal).Select(item => new { item.Key, item.Value })
            })
            .OrderBy(policy => policy.Id, StringComparer.Ordinal)
            .ThenBy(policy => JsonSerializer.Serialize(policy), StringComparer.Ordinal)
            .ToList();
        var window = governanceWindow;
        var semantic = new
        {
            Version = 1,
            Policies = policies,
            RuntimeMode = mode.ToString(),
            GovernanceWindow = window is null ? null : new
            {
                window.Name,
                window.Description,
                window.Enabled,
                Mode = window.Mode.ToString(),
                AffectedCapabilities = window.AffectedCapabilities
                    .OrderBy(value => value, StringComparer.Ordinal).ToList(),
                window.Reason
            }
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(semantic));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }
}