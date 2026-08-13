using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;

namespace Seneschal.Api.Services;

public sealed class OperatorGovernanceContextService
{
    private readonly PolicyLoader _policies;
    private readonly ICapabilityCatalog _capabilities;

    public OperatorGovernanceContextService(
        PolicyLoader policies,
        ICapabilityCatalog capabilities)
    {
        _policies = policies;
        _capabilities = capabilities;
    }

    public async Task<IReadOnlyCollection<ConfiguredCapabilityContext>>
        GetIdentityCapabilitiesAsync(
            string identityId,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);
        var results = new List<ConfiguredCapabilityContext>();

        foreach (var policy in _policies.GetPolicies()
            .Where(policy => policy.EffectiveIdentities.Contains(
                identityId, StringComparer.OrdinalIgnoreCase)))
        {
            foreach (var capabilityId in policy.EffectiveCapabilities)
            {
                var entry = await _capabilities.GetByIdAsync(
                    capabilityId, cancellationToken);
                results.Add(new ConfiguredCapabilityContext(
                    capabilityId,
                    entry?.Capability.DisplayName ?? capabilityId,
                    entry?.Capability.Technology ?? string.Empty,
                    entry?.Capability.Category ?? string.Empty,
                    entry?.Capability.RiskLevel.ToString() ?? "Unknown",
                    FormatProvenance(entry?.Provenance ?? []),
                    policy.Name,
                    policy.Decision,
                    policy.Reason,
                    policy.EffectiveEnvironments));
            }
        }

        return results
            .OrderBy(item => RiskOrder(item.Risk))
            .ThenBy(item => item.CapabilityId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.PolicyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatProvenance(
        IReadOnlyCollection<CapabilityProvenance> sources)
    {
        var labels = sources.Select(source => source.Kind == "CapabilityPack"
                ? $"Pack: {source.PackId} {source.PackVersion}"
                : "Local catalog")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return labels.Count switch
        {
            0 => "Unknown",
            1 => labels[0],
            _ => $"Multiple sources: {string.Join(", ", labels)}"
        };
    }

    private static int RiskOrder(string risk) => risk.ToLowerInvariant() switch
    {
        "critical" => 0,
        "high" => 1,
        "medium" => 2,
        "low" => 3,
        _ => 4
    };
}

public sealed record ConfiguredCapabilityContext(
    string CapabilityId,
    string DisplayName,
    string Technology,
    string Category,
    string Risk,
    string Provenance,
    string PolicyName,
    string Decision,
    string Reason,
    IReadOnlyCollection<string> Environments);