using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class IntegrationApiKeyLoaderTests :
    IClassFixture<ApiApplicationFactory>
{
    public IntegrationApiKeyLoaderTests(ApiApplicationFactory factory)
    {
        _ = factory;
    }

    [Fact]
    public void GetKeys_LoadsSampleYamlConfiguration()
    {
        var loader = new IntegrationApiKeyLoader();

        var keys = loader.GetKeys();

        Assert.Contains(
            keys,
            key => key.Name == "capability-control-demo" &&
                key.Key == "dev-capability-control-key" &&
                key.AllowedIdentities.Contains("platform-agent") &&
                key.AllowedCapabilities.Contains(
                    "infrastructure.production.apply"));
    }
}
