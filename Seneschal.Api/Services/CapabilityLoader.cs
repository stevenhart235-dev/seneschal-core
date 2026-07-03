using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public class CapabilityLoader
{
    private readonly List<Capability> _capabilities;

    public CapabilityLoader()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Policies",
            "capabilities.yaml");

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