using Seneschal.Api.Mappers;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using ApiCapability = Seneschal.Api.Models.Capability;
using ApiIdentity = Seneschal.Api.Models.IdentityDefinition;
using ApiPolicy = Seneschal.Api.Models.Policy;

namespace Seneschal.Api.Services;

public sealed class ConfigurationValidator : IConfigurationValidator
{
    private readonly CapabilityLoader _capabilityLoader;
    private readonly IdentityLoader _identityLoader;
    private readonly PolicyLoader _policyLoader;
    private readonly RuntimeSettings _runtimeSettings;

    public ConfigurationValidator(
        CapabilityLoader capabilityLoader,
        IdentityLoader identityLoader,
        PolicyLoader policyLoader,
        RuntimeSettings runtimeSettings)
    {
        _capabilityLoader = capabilityLoader;
        _identityLoader = identityLoader;
        _policyLoader = policyLoader;
        _runtimeSettings = runtimeSettings;
    }

    public ConfigurationValidationResult Validate()
    {
        return Validate(
            _capabilityLoader.GetCapabilities(),
            _identityLoader.GetIdentities(),
            _policyLoader.GetPolicies(),
            _runtimeSettings);
    }

    public static ConfigurationValidationResult Validate(
        IReadOnlyCollection<ApiCapability> capabilities,
        IReadOnlyCollection<ApiIdentity> identities,
        IReadOnlyCollection<ApiPolicy> policies,
        RuntimeSettings runtimeSettings)
    {
        var findings = new List<ConfigurationValidationFinding>();

        AddLoadFinding(findings, "Capabilities", capabilities.Count);
        AddLoadFinding(findings, "Identities", identities.Count);
        AddLoadFinding(findings, "Policies", policies.Count);
        ValidateRuntimeSettings(findings, runtimeSettings);
        ValidatePolicyReferences(findings, capabilities, identities, policies);
        ValidatePolicyDecisions(findings, policies);
        ValidateDuplicatePolicies(findings, policies);
        ValidateOrphans(findings, capabilities, identities, policies);

        return new ConfigurationValidationResult
        {
            Findings = findings
        };
    }

    private static void AddLoadFinding(
        ICollection<ConfigurationValidationFinding> findings,
        string category,
        int count)
    {
        if (count > 0)
        {
            return;
        }

        findings.Add(new ConfigurationValidationFinding
        {
            Severity = "Error",
            Category = category,
            Message = $"{category} configuration loaded but contains no entries."
        });
    }

    private static void ValidateRuntimeSettings(
        ICollection<ConfigurationValidationFinding> findings,
        RuntimeSettings runtimeSettings)
    {
        if (Enum.IsDefined(typeof(EnforcementMode), runtimeSettings.Mode))
        {
            return;
        }

        findings.Add(new ConfigurationValidationFinding
        {
            Severity = "Error",
            Category = "RuntimeSettings",
            Message = $"Runtime enforcement mode '{runtimeSettings.Mode}' is not valid."
        });
    }

    private static void ValidatePolicyReferences(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiCapability> capabilities,
        IReadOnlyCollection<ApiIdentity> identities,
        IReadOnlyCollection<ApiPolicy> policies)
    {
        var capabilityNames = capabilities
            .Select(capability => capability.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var identityNames = identities
            .Select(identity => identity.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var policy in policies)
        {
            if (!capabilityNames.Contains(policy.Capability))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "PolicyReference",
                    Message =
                        $"Policy '{policy.Name}' references unknown capability '{policy.Capability}'.",
                    RelatedObjectId = policy.Name
                });
            }

            if (!identityNames.Contains(policy.Identity))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "PolicyReference",
                    Message =
                        $"Policy '{policy.Name}' references unknown identity '{policy.Identity}'.",
                    RelatedObjectId = policy.Name
                });
            }
        }
    }

    private static void ValidatePolicyDecisions(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiPolicy> policies)
    {
        foreach (var policy in policies)
        {
            try
            {
                _ = DecisionTypeMapper.ToCore(policy.Decision);
            }
            catch (ArgumentException)
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "PolicyDecision",
                    Message =
                        $"Policy '{policy.Name}' uses invalid decision '{policy.Decision}'.",
                    RelatedObjectId = policy.Name
                });
            }
        }
    }

    private static void ValidateDuplicatePolicies(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiPolicy> policies)
    {
        foreach (var duplicatePolicy in policies
            .GroupBy(policy => policy.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            findings.Add(new ConfigurationValidationFinding
            {
                Severity = "Error",
                Category = "PolicyIdentity",
                Message = $"Duplicate policy id '{duplicatePolicy.Key}' detected.",
                RelatedObjectId = duplicatePolicy.Key
            });
        }
    }

    private static void ValidateOrphans(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiCapability> capabilities,
        IReadOnlyCollection<ApiIdentity> identities,
        IReadOnlyCollection<ApiPolicy> policies)
    {
        var referencedCapabilities = policies
            .Select(policy => policy.Capability)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referencedIdentities = policies
            .Select(policy => policy.Identity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var capability in capabilities
            .Where(capability => !referencedCapabilities.Contains(capability.Name)))
        {
            findings.Add(new ConfigurationValidationFinding
            {
                Severity = "Info",
                Category = "OrphanedCapability",
                Message =
                    $"Capability '{capability.Name}' is not referenced by any policy.",
                RelatedObjectId = capability.Name
            });
        }

        foreach (var identity in identities
            .Where(identity => !referencedIdentities.Contains(identity.Name)))
        {
            findings.Add(new ConfigurationValidationFinding
            {
                Severity = "Info",
                Category = "OrphanedIdentity",
                Message =
                    $"Identity '{identity.Name}' is not referenced by any policy.",
                RelatedObjectId = identity.Name
            });
        }
    }
}
