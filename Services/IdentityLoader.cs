using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public class IdentityLoader
{
    private readonly List<IdentityDefinition> _identities;

    public IdentityLoader()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Policies",
            "identities.yaml");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Identity file not found: {path}");

        var yaml = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var identityFile = deserializer.Deserialize<IdentityFile>(yaml);

        _identities = identityFile.Identities;
    }

    public IReadOnlyList<IdentityDefinition> GetIdentities()
    {
        return _identities;
    }
}