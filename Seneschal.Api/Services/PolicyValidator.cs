using Seneschal.Api.Mappers;

namespace Seneschal.Api.Services;

public class PolicyValidator
{
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

            try
            {
                _ = DecisionTypeMapper.ToCore(policy.Decision);
            }
            catch (ArgumentException)
            {
                errors.Add(
                    $"Policy '{policy.Name}' has invalid decision " +
                    $"'{policy.Decision}'. Valid decisions: allow, deny, " +
                    "warn, log_only, requires_approval.");
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
