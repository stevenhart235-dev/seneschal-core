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
        var policies = _policies.GetCorePolicies()
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
        var window = _window?.GetWindow();
        var semantic = new
        {
            Version = 1,
            Policies = policies,
            RuntimeMode = _mode.GetMode().ToString(),
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