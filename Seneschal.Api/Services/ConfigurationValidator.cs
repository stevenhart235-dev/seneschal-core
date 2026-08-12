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
        ValidateCapabilityMetadata(findings, capabilities);
        ValidateDuplicateCapabilities(findings, capabilities);
        ValidateIdentityMetadata(findings, identities);
        ValidateDuplicateIdentities(findings, identities);
        ValidatePolicyReferences(findings, capabilities, identities, policies);
        ValidateRequiredPolicyFields(findings, policies);
        ValidatePolicyDecisions(findings, policies);
        ValidateDuplicatePolicies(findings, policies);
        ValidateOrphans(findings, capabilities, identities, policies);

        return new ConfigurationValidationResult
        {
            Findings = findings
        };
    }

    private static void ValidateCapabilityMetadata(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiCapability> capabilities)
    {
        foreach (var capability in capabilities)
        {
            var capabilityId = string.IsNullOrWhiteSpace(capability.Name)
                ? "<unknown>"
                : capability.Name;

            if (string.IsNullOrWhiteSpace(capability.Name))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "CapabilityIdentity",
                    Message = "Capability is missing required field 'name'.",
                    RelatedObjectId = capabilityId
                });
            }

            if (!CapabilityMapper.TryParseRiskLevel(capability.Risk, out _))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "CapabilityMetadata",
                    Message =
                        $"Capability '{capabilityId}' uses invalid risk level " +
                        $"'{capability.Risk}'. Expected Low, Medium, High, or Critical.",
                    RelatedObjectId = capabilityId
                });
            }

            if (!string.IsNullOrWhiteSpace(capability.DocumentationUrl) &&
                (!Uri.TryCreate(capability.DocumentationUrl, UriKind.Absolute,
                    out var documentationUri) ||
                 documentationUri.Scheme is not ("http" or "https")))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Warning",
                    Category = "CapabilityMetadata",
                    Message =
                        $"Capability '{capabilityId}' has an invalid documentationUrl.",
                    RelatedObjectId = capabilityId
                });
            }

            var tags = capability.Tags ?? [];
            if (tags.Any(string.IsNullOrWhiteSpace))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Warning",
                    Category = "CapabilityMetadata",
                    Message = $"Capability '{capabilityId}' contains a blank tag.",
                    RelatedObjectId = capabilityId
                });
            }

            if (tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Warning",
                    Category = "CapabilityMetadata",
                    Message = $"Capability '{capabilityId}' contains duplicate tags.",
                    RelatedObjectId = capabilityId
                });
            }
        }
    }

    private static void ValidateDuplicateCapabilities(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiCapability> capabilities)
    {
        foreach (var duplicateCapability in capabilities
            .Where(capability => !string.IsNullOrWhiteSpace(capability.Name))
            .GroupBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            findings.Add(new ConfigurationValidationFinding
            {
                Severity = "Error",
                Category = "CapabilityIdentity",
                Message =
                    $"Duplicate capability id '{duplicateCapability.Key}' detected.",
                RelatedObjectId = duplicateCapability.Key
            });
        }
    }

    private static void ValidateRequiredPolicyFields(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiPolicy> policies)
    {
        foreach (var policy in policies)
        {
            AddRequiredPolicyFinding(findings, policy, "name", policy.Name);
            AddRequiredPolicyFinding(findings, policy, "decision", policy.Decision);
            AddRequiredPolicyFinding(findings, policy, "reason", policy.Reason);

            if (policy.EffectiveIdentities.Count == 0)
                AddMissingPolicyTargetFinding(findings, policy, "identity");
            if (policy.EffectiveCapabilities.Count == 0)
                AddMissingPolicyTargetFinding(findings, policy, "capability");
            if (policy.EffectiveEnvironments.Count == 0)
                AddMissingPolicyTargetFinding(findings, policy, "environment");
        }
    }

    private static void AddRequiredPolicyFinding(
        ICollection<ConfigurationValidationFinding> findings,
        ApiPolicy policy,
        string field,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) return;

        findings.Add(new ConfigurationValidationFinding
        {
            Severity = "Error",
            Category = "PolicyStructure",
            Message = $"Policy '{PolicyId(policy)}' is missing required field '{field}'.",
            RelatedObjectId = PolicyId(policy)
        });
    }

    private static void AddMissingPolicyTargetFinding(
        ICollection<ConfigurationValidationFinding> findings,
        ApiPolicy policy,
        string target)
    {
        findings.Add(new ConfigurationValidationFinding
        {
            Severity = "Error",
            Category = "PolicyStructure",
            Message = $"Policy '{PolicyId(policy)}' must reference at least one {target}.",
            RelatedObjectId = PolicyId(policy)
        });
    }

    private static string PolicyId(ApiPolicy policy) =>
        string.IsNullOrWhiteSpace(policy.Name) ? "<unknown>" : policy.Name;

    private static void ValidateDuplicateIdentities(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiIdentity> identities)
    {
        foreach (var duplicateIdentity in identities
            .GroupBy(identity => identity.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            findings.Add(new ConfigurationValidationFinding
            {
                Severity = "Error",
                Category = "IdentityIdentity",
                Message =
                    $"Duplicate identity id '{duplicateIdentity.Key}' detected.",
                RelatedObjectId = duplicateIdentity.Key
            });
        }
    }

    private static void ValidateIdentityMetadata(
        ICollection<ConfigurationValidationFinding> findings,
        IReadOnlyCollection<ApiIdentity> identities)
    {
        foreach (var identity in identities)
        {
            ValidateOptionalIdentityValue(
                findings, identity, "displayName", identity.DisplayName);
            ValidateOptionalIdentityValue(
                findings, identity, "owner", identity.Owner);
            ValidateOptionalIdentityValue(
                findings, identity, "application", identity.Application);
            ValidateOptionalIdentityValue(
                findings, identity, "environment", identity.Environment);
            ValidateOptionalIdentityValue(
                findings, identity, "technology", identity.Technology);
            ValidateOptionalIdentityValue(
                findings, identity, "description", identity.Description);
        }
    }

    private static void ValidateOptionalIdentityValue(
        ICollection<ConfigurationValidationFinding> findings,
        ApiIdentity identity,
        string property,
        string? value)
    {
        if (value is null || !string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        findings.Add(new ConfigurationValidationFinding
        {
            Severity = "Warning",
            Category = "IdentityMetadata",
            Message =
                $"Identity '{identity.Name}' has a blank optional '{property}' value.",
            RelatedObjectId = identity.Name
        });
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
            Message = $"Runtime mode '{runtimeSettings.Mode}' is not valid."
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
            foreach (var capability in policy.EffectiveCapabilities
                .Where(capability => !capabilityNames.Contains(capability)))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "PolicyReference",
                    Message =
                        $"Policy '{policy.Name}' references unknown capability '{capability}'.",
                    RelatedObjectId = policy.Name
                });
            }

            foreach (var identity in policy.EffectiveIdentities
                .Where(identity => !identityNames.Contains(identity)))
            {
                findings.Add(new ConfigurationValidationFinding
                {
                    Severity = "Error",
                    Category = "PolicyReference",
                    Message =
                        $"Policy '{policy.Name}' references unknown identity '{identity}'.",
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
            .SelectMany(policy => policy.EffectiveCapabilities)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var referencedIdentities = policies
            .SelectMany(policy => policy.EffectiveIdentities)
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
