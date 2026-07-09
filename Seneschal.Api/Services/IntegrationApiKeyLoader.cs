using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public sealed class IntegrationApiKeyLoader
{
    private readonly IReadOnlyList<IntegrationApiKey> _keys;

    public IntegrationApiKeyLoader()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Policies",
            "integration-keys.yaml");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Integration API key file not found: {path}");
        }

        var yaml = File.ReadAllText(path);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var keyFile = deserializer.Deserialize<IntegrationApiKeyFile>(yaml);

        _keys = keyFile.IntegrationKeys;
    }

    private IntegrationApiKeyLoader(
        IEnumerable<IntegrationApiKey> keys)
    {
        _keys = keys.ToList();
    }

    public static IntegrationApiKeyLoader FromKeys(
        IEnumerable<IntegrationApiKey> keys)
    {
        return new IntegrationApiKeyLoader(keys);
    }

    public IReadOnlyList<IntegrationApiKey> GetKeys()
    {
        return _keys;
    }
}
