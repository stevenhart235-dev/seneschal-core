using System.Text.Json;
using Json.Schema;
using Seneschal.Api.Models;
using ApiPolicy = Seneschal.Api.Models.Policy;

namespace Seneschal.Api.Services;

public sealed class ProposedPolicyConfigurationService
{
    private readonly CapabilityLoader _capabilities;
    private readonly IdentityLoader _identities;
    private readonly RuntimeSettings _runtime;
    private static readonly object SchemaLock = new();
    private static JsonSchema? _sharedPolicySchema;
    private readonly JsonSchema _policySchema;

    public ProposedPolicyConfigurationService(CapabilityLoader capabilities,
        IdentityLoader identities, RuntimeSettings runtime, IHostEnvironment environment)
    {
        _capabilities = capabilities;
        _identities = identities;
        _runtime = runtime;
        var path = Path.Combine(environment.ContentRootPath, "..", "integrations",
            "contracts", "policy", "policy-schema.v1.json");
        if (!File.Exists(path)) path = Path.Combine(AppContext.BaseDirectory,
            "contracts", "policy", "policy-schema.v1.json");
        lock (SchemaLock)
            _policySchema = _sharedPolicySchema ??= JsonSchema.FromText(File.ReadAllText(Path.GetFullPath(path)),
                new BuildOptions { SchemaRegistry = new SchemaRegistry() });
    }

    public ProposedPolicyConfigurationResult Apply(
        ProposedGovernanceChange proposal, IReadOnlyList<ApiPolicy> currentPolicies)
    {
        if (proposal.Change.Operation != "RemoveCapabilityFromPolicy")
            return Failed($"Unsupported proposal operation '{proposal.Change.Operation}'.");
        var matches = currentPolicies.Where(policy => string.Equals(policy.Name,
            proposal.Change.Policy, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1)
            return Failed(matches.Count == 0
                ? $"Policy '{proposal.Change.Policy}' was not found."
                : $"Policy '{proposal.Change.Policy}' is not unique.");
        var policy = matches[0];
        if (!string.Equals(proposal.Source.Capability, proposal.Change.Capability,
            StringComparison.OrdinalIgnoreCase))
            return Failed("Proposal source capability does not match the semantic change target.");
        if (!policy.EffectiveIdentities.Contains(proposal.Source.Identity,
            StringComparer.OrdinalIgnoreCase))
            return Failed($"Policy '{policy.Name}' does not target source identity '{proposal.Source.Identity}'.");
        if (!policy.EffectiveCapabilities.Contains(proposal.Change.Capability,
            StringComparer.OrdinalIgnoreCase))
            return Failed($"Policy '{policy.Name}' does not explicitly target capability '{proposal.Change.Capability}'.");

        var proposed = currentPolicies.Select(Clone).ToList();
        var target = proposed.Single(item => string.Equals(item.Name, policy.Name,
            StringComparison.OrdinalIgnoreCase));
        if (string.Equals(target.Capability, proposal.Change.Capability,
            StringComparison.OrdinalIgnoreCase)) target.Capability = "";
        target.Capabilities = target.Capabilities.Where(value => !string.Equals(value,
            proposal.Change.Capability, StringComparison.OrdinalIgnoreCase)).ToList();
        if (target.EffectiveCapabilities.Count == 0)
            return Failed("Removing the capability would leave the policy without a capability target.");

        var schemaErrors = ValidatePolicySchema(proposed);
        if (schemaErrors.Count > 0) return new(false, proposed, schemaErrors);
        var validation = ConfigurationValidator.Validate(_capabilities.GetCapabilities(),
            _identities.GetIdentities(), proposed, _runtime);
        var errors = validation.Findings.Where(item => item.Severity == "Error")
            .Select(item => item.Message).ToList();
        return errors.Count == 0 ? new(true, proposed, []) : new(false, proposed, errors);
    }

    private IReadOnlyCollection<string> ValidatePolicySchema(IReadOnlyList<ApiPolicy> policies)
    {
        var root = new Dictionary<string, object?>
        { ["policies"] = policies.Select(ToContractPolicy).ToList() };
        var result = _policySchema.Evaluate(JsonSerializer.SerializeToElement(root),
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        return result.Details?.Where(detail => !detail.IsValid && detail.Errors?.Count > 0)
            .SelectMany(detail => detail.Errors!.Values).Distinct().ToList()
            ?? ["Proposed policies do not conform to Policy Schema v1."];
    }

    private static Dictionary<string, object?> ToContractPolicy(ApiPolicy policy)
    {
        var result = new Dictionary<string, object?> { ["name"] = policy.Name,
            ["decision"] = policy.Decision, ["reason"] = policy.Reason };
        Add(result, "displayName", policy.DisplayName); Add(result, "description", policy.Description);
        Add(result, "owner", policy.Owner); Add(result, "severity", policy.Severity);
        Add(result, "rationale", policy.Rationale); Add(result, "identity", policy.Identity);
        Add(result, "capability", policy.Capability); Add(result, "environment", policy.Environment);
        if (policy.Identities.Count > 0) result["identities"] = policy.Identities;
        if (policy.Capabilities.Count > 0) result["capabilities"] = policy.Capabilities;
        if (policy.Environments.Count > 0) result["environments"] = policy.Environments;
        return result;
    }
    private static void Add(IDictionary<string, object?> values, string key, string value)
    { if (!string.IsNullOrWhiteSpace(value)) values[key] = value; }
    private static ApiPolicy Clone(ApiPolicy p) => new() { Name=p.Name, DisplayName=p.DisplayName,
        Description=p.Description, Owner=p.Owner, Severity=p.Severity, Rationale=p.Rationale,
        Identity=p.Identity, Identities=[..p.Identities], Capability=p.Capability,
        Capabilities=[..p.Capabilities], Environment=p.Environment, Environments=[..p.Environments],
        Decision=p.Decision, Reason=p.Reason };
    private static ProposedPolicyConfigurationResult Failed(string error) => new(false, [], [error]);
}

public sealed record ProposedPolicyConfigurationResult(bool IsValid,
    IReadOnlyList<ApiPolicy> Policies, IReadOnlyCollection<string> Errors);
