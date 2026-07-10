using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public class CapabilityLoader
{
    private readonly List<Capability> _capabilities;

    public CapabilityLoader()
        : this(YamlConfigurationPathResolver.Resolve(
            AppContext.BaseDirectory,
            configuredPath: null,
            "capabilities.yaml"))
    {
    }

    public CapabilityLoader(
        IHostEnvironment environment,
        IConfiguration configuration)
        : this(YamlConfigurationPathResolver.Resolve(
            environment.ContentRootPath,
            configuration[YamlConfigurationPathResolver.CapabilitiesPathKey],
            "capabilities.yaml"))
    {
    }

    public CapabilityLoader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var yaml = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var capabilityFile =
            deserializer.Deserialize<CapabilityFile>(yaml);

        _capabilities = capabilityFile.Capabilities;
    }

    public IReadOnlyList<Capability> GetCapabilities()
        => _capabilities;
}
