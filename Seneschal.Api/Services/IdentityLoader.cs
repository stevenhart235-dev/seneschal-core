using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public class IdentityLoader
{
    private readonly List<IdentityDefinition> _identities;

    public IdentityLoader()
        : this(YamlConfigurationPathResolver.Resolve(
            AppContext.BaseDirectory,
            configuredPath: null,
            "identities.yaml"))
    {
    }

    public IdentityLoader(
        IHostEnvironment environment,
        IConfiguration configuration)
        : this(YamlConfigurationPathResolver.Resolve(
            environment.ContentRootPath,
            configuration[YamlConfigurationPathResolver.IdentitiesPathKey],
            "identities.yaml"))
    {
    }

    public IdentityLoader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

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
