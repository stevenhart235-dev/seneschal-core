using Seneschal.Api.Models;

namespace Seneschal.Api.Services;

public class PolicyValidator
{
    private static readonly HashSet<string> ValidDecisions = new(
        new[] { "allow", "deny", "requires_approval" },
        StringComparer.OrdinalIgnoreCase);

    public PolicyValidator(
        PolicyLoader policyLoader,
        CapabilityLoader capabilityLoader,
        IdentityLoader identityLoader)
    {
        var policies = policyLoader.GetPolicies();
        var capabilities = capabilityLoader.GetCapabilities();
        var identities = identityLoader.GetIdentities();

        var capabilityNames = capabilities
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var identityNames = identities
            .Select(i => i.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();

        foreach (var policy in policies)
        {
            if (!identityNames.Contains(policy.Identity))
            {
                errors.Add($"Policy '{policy.Name}' references unknown identity '{policy.Identity}'.");
            }

            if (!capabilityNames.Contains(policy.Capability))
            {
                errors.Add($"Policy '{policy.Name}' references unknown capability '{policy.Capability}'.");
            }

            if (!ValidDecisions.Contains(policy.Decision))
            {
                errors.Add($"Policy '{policy.Name}' has invalid decision '{policy.Decision}'. Valid decisions: allow, deny, requires_approval.");
            }

            if (string.IsNullOrWhiteSpace(policy.Environment))
            {
                errors.Add($"Policy '{policy.Name}' is missing environment.");
            }
        }

        if (errors.Any())
        {
            throw new InvalidOperationException(
                "Policy validation failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors));
        }
    }
}