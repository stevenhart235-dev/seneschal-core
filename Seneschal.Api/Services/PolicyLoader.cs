using Seneschal.Api.Mappers;
using Seneschal.Api.Models;
using Seneschal.Core.Enums;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ApiPolicy = Seneschal.Api.Models.Policy;
using CorePolicy = Seneschal.Core.Models.Policy;

namespace Seneschal.Api.Services;

public class PolicyLoader
{
    private readonly List<ApiPolicy> _policies;
    private readonly Lazy<IReadOnlyList<CorePolicy>> _corePolicies;

    public PolicyLoader()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Policies", "policies.yaml");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Policy file not found: {path}");

        var yaml = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var policyFile = deserializer.Deserialize<PolicyFile>(yaml);

        _policies = policyFile.Policies;
        _corePolicies = new Lazy<IReadOnlyList<CorePolicy>>(
            ProjectCorePolicies);
    }

    public IReadOnlyList<ApiPolicy> GetPolicies()
    {
        return _policies;
    }

    public IReadOnlyList<CorePolicy> GetCorePolicies()
    {
        return _corePolicies.Value;
    }

    private IReadOnlyList<CorePolicy> ProjectCorePolicies()
    {
        var projectedPolicies = _policies
            .Select((policy, index) => new CorePolicy
            {
                Id = policy.Name,
                Name = policy.Name,
                Effect = DecisionTypeMapper.ToCore(policy.Decision),
                Reason = policy.Reason,
                Priority = _policies.Count - index,
                Conditions = new Dictionary<string, string>
                {
                    ["identity.id"] = policy.Identity,
                    ["capability.id"] = policy.Capability,
                    ["resource.environment"] = policy.Environment
                }
            })
            .ToList();

        projectedPolicies.Add(new CorePolicy
        {
            Id = "default-deny",
            Name = "default-deny",
            Effect = DecisionType.Deny,
            Reason = "No matching allow policy found",
            Priority = int.MinValue
        });

        return projectedPolicies;
    }
}
