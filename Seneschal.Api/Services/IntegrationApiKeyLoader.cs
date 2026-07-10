using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public sealed class IntegrationApiKeyLoader
{
    private readonly IReadOnlyList<IntegrationApiKey> _keys;

    public IntegrationApiKeyLoader()
        : this(YamlConfigurationPathResolver.Resolve(
            AppContext.BaseDirectory,
            configuredPath: null,
            "integration-keys.yaml"))
    {
    }

    public IntegrationApiKeyLoader(
        IHostEnvironment environment,
        IConfiguration configuration)
        : this(YamlConfigurationPathResolver.Resolve(
            environment.ContentRootPath,
            configuration[
                YamlConfigurationPathResolver.IntegrationKeysPathKey],
            "integration-keys.yaml"))
    {
    }

    public IntegrationApiKeyLoader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

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
