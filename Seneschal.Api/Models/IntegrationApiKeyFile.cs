namespace Seneschal.Api.Models;

public sealed class IntegrationApiKeyFile
{
    public List<IntegrationApiKey> IntegrationKeys { get; set; } = new();
}
