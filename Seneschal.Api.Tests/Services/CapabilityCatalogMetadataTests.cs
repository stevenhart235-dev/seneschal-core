using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests.Services;

public sealed class CapabilityCatalogMetadataTests
{
    [Fact]
    public void LoaderAndMapper_LoadCapabilityMetadata()
    {
        var path = WriteCapabilities(
            """
            capabilities:
              - name: production.deployment.execute
                displayName: Execute Production Deployment
                description: Execute a production deployment.
                owner: Release Engineering
                risk: High
                category: Deployment
                lifecycle: Active
                documentationUrl: https://example.test/capabilities/deploy
                tags: [production, deployment]
            """);

        try
        {
            var capability = CapabilityMapper.ToCore(
                Assert.Single(new CapabilityLoader(path).GetCapabilities()));

            Assert.Equal("production.deployment.execute", capability.Id);
            Assert.Equal("Execute Production Deployment", capability.DisplayName);
            Assert.Equal("Release Engineering", capability.Owner);
            Assert.Equal(RiskLevel.High, capability.RiskLevel);
            Assert.Equal("Deployment", capability.Category);
            Assert.Equal("Active", capability.Lifecycle);
            Assert.Equal(
                "https://example.test/capabilities/deploy",
                capability.DocumentationUrl);
            Assert.Equal(["production", "deployment"], capability.Tags);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Mapper_PreservesIdentifierOnlyCapabilityCompatibility()
    {
        var path = WriteCapabilities(
            """
            capabilities:
              - name: legacy.capability
            """);

        try
        {
            var capability = CapabilityMapper.ToCore(
                Assert.Single(new CapabilityLoader(path).GetCapabilities()));

            Assert.Equal("legacy.capability", capability.Id);
            Assert.Equal("legacy.capability", capability.DisplayName);
            Assert.Equal(RiskLevel.Low, capability.RiskLevel);
            Assert.Equal("Uncategorized", capability.Category);
            Assert.Equal("Active", capability.Lifecycle);
            Assert.Empty(capability.Tags);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteCapabilities(string yaml)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"seneschal-capabilities-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }
}
