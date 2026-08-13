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
        : this(YamlConfigurationPathResolver.Resolve(
            AppContext.BaseDirectory,
            configuredPath: null,
            "policies.yaml"))
    {
    }

    public PolicyLoader(
        IHostEnvironment environment,
        IConfiguration configuration)
        : this(YamlConfigurationPathResolver.Resolve(
            environment.ContentRootPath,
            configuration[YamlConfigurationPathResolver.PoliciesPathKey],
            "policies.yaml"))
    {
    }

    public PolicyLoader(string path)
        : this(path, rejectUnmatchedProperties: false)
    {
    }

    public PolicyLoader(string path, bool rejectUnmatchedProperties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Policy file not found: {path}");

        var yaml = File.ReadAllText(path);

        var builder = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance);
        if (!rejectUnmatchedProperties)
            builder = builder.IgnoreUnmatchedProperties();
        var deserializer = builder.Build();

        var policyFile = deserializer.Deserialize<PolicyFile>(yaml);

        _policies = policyFile.Policies;
        _corePolicies = new Lazy<IReadOnlyList<CorePolicy>>(
            () => ProjectCorePolicies(_policies));
    }

    public IReadOnlyList<ApiPolicy> GetPolicies()
    {
        return _policies;
    }

    public IReadOnlyList<CorePolicy> GetCorePolicies()
    {
        return _corePolicies.Value;
    }

    public static IReadOnlyList<CorePolicy> ProjectCorePolicies(IReadOnlyList<ApiPolicy> policies)
    {
        var projectedPolicies = policies
            .SelectMany((policy, index) =>
                from identity in policy.EffectiveIdentities
                from capability in policy.EffectiveCapabilities
                from environment in policy.EffectiveEnvironments
                select new CorePolicy
                {
                    Id = policy.Name,
                    Name = policy.Name,
                    Effect = DecisionTypeMapper.ToCore(policy.Decision),
                    Reason = policy.Reason,
                    Priority = policies.Count - index,
                    Conditions = new Dictionary<string, string>
                    {
                        ["identity.id"] = identity,
                        ["capability.id"] = capability,
                        ["resource.environment"] = environment
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
