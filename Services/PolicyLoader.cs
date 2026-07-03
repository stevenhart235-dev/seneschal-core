using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public class PolicyLoader
{
    private readonly List<Policy> _policies;

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
    }

    public IReadOnlyList<Policy> GetPolicies()
    {
        return _policies;
    }
}