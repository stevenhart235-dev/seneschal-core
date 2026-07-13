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
        Assert.Contains(
            keys,
            key => key.Name == "github-actions-production-development" &&
                key.Key == "dev-github-actions-key" &&
                key.Environment == "production" &&
                key.AllowedIdentities.SequenceEqual(["github-actions-production"]) &&
                key.AllowedCapabilities.SequenceEqual(["production.deployment.execute"]));
        Assert.Contains(
            keys,
            key => key.Name == "terraform-production-development" &&
                key.Key == "dev-terraform-production-key" &&
                key.Environment == "production" &&
                key.AllowedIdentities.SequenceEqual(["terraform-production"]) &&
                key.AllowedCapabilities.SequenceEqual(["infrastructure.production.apply"]));
    }
}
