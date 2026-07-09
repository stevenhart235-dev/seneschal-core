using YamlDotNet.Serialization;

namespace Seneschal.Api.Models;

public sealed class IntegrationApiKeyFile
{
    [YamlMember(Alias = "integrationKeys")]
    public List<IntegrationApiKey> IntegrationKeys { get; set; } = new();
}
