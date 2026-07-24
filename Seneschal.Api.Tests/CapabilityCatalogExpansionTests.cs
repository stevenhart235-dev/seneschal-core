using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityCatalogExpansionTests
{
    private static readonly HashSet<string> IntentionalLegacyUnclassified =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "DeployApplication",
            "DeleteProductionDatabase"
        };

    [Fact]
    public void ExpandedCatalogLoadsWithUniqueStableIdentifiers()
    {
        var capabilities = new CapabilityLoader().GetCapabilities();

        Assert.InRange(capabilities.Count, 40, 60);
        Assert.Equal(
            capabilities.Count,
            capabilities.Select(item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.All(capabilities, capability =>
            Assert.False(string.IsNullOrWhiteSpace(capability.Name)));
    }

    [Fact]
    public void EnterpriseCapabilitiesHaveRequiredMetadata()
    {
        var capabilities = new CapabilityLoader().GetCapabilities()
            .Where(item => !IntentionalLegacyUnclassified.Contains(item.Name));

        Assert.All(capabilities, capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(capability.Description));
            Assert.False(string.IsNullOrWhiteSpace(capability.Owner));
            Assert.False(string.IsNullOrWhiteSpace(capability.Risk));
            Assert.False(string.IsNullOrWhiteSpace(capability.Technology));
            Assert.False(string.IsNullOrWhiteSpace(capability.Category));
            Assert.False(string.IsNullOrWhiteSpace(capability.Lifecycle));
            Assert.NotEmpty(capability.Tags);
        });
    }

    [Fact]
    public void ExpectedTechnologyGroupsAndRiskLevelsAreRepresented()
    {
        var capabilities = new CapabilityLoader().GetCapabilities();
        var technologies = capabilities
            .Select(item => item.Technology)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var risks = capabilities
            .Select(item => item.Risk)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Subset(
            technologies,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "azure", "github", "terraform", "kubernetes", "openai",
                "postgresql", "slack", "m365", "custom"
            });
        Assert.Subset(
            risks,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Low", "Medium", "High", "Critical"
            });
    }

    [Theory]
    [InlineData("azure.resourcegroup.delete")]
    [InlineData("github.repository.secret.write")]
    [InlineData("infrastructure.production.destroy")]
    [InlineData("kubernetes.namespace.delete")]
    [InlineData("openai.production-secret.read")]
    [InlineData("postgres.table.drop")]
    [InlineData("breakglass.activate")]
    public void KnownDestructiveCapabilitiesAreCritical(string capabilityId)
    {
        var capability = new CapabilityLoader().GetCapabilities()
            .Single(item => item.Name == capabilityId);

        Assert.Equal(
            RiskLevel.Critical,
            CapabilityMapper.ToCore(capability).RiskLevel);
    }

    [Theory]
    [InlineData("azure.keyvault.secret.write", "azure")]
    [InlineData("github.workflow.dispatch", "github")]
    [InlineData("terraform.plan", "terraform")]
    [InlineData("kubernetes.pod.logs.read", "kubernetes")]
    [InlineData("openai.model.invoke", "openai")]
    [InlineData("postgres.schema.migrate", "postgresql")]
    [InlineData("slack.security-alert.send", "slack")]
    [InlineData("m365.sharepoint.document.read", "m365")]
    [InlineData("breakglass.activate", "custom")]
    public void ExplicitMetadataClassifiesIntoExpectedTechnology(
        string capabilityId,
        string expectedTechnology)
    {
        var configured = new CapabilityLoader().GetCapabilities()
            .Single(item => item.Name == capabilityId);
        var capability = CapabilityMapper.ToCore(configured);

        Assert.Equal(
            expectedTechnology,
            new TechnologyClassifier().Classify(capability.Id, capability).Key);
    }

    [Theory]
    [InlineData("DeployApplication")]
    [InlineData("DeleteProductionDatabase")]
    [InlineData("azure.keyvault.secret.read")]
    [InlineData("infrastructure.production.apply")]
    [InlineData("infrastructure.production.destroy")]
    [InlineData("production.deployment.execute")]
    [InlineData("database.migration.execute")]
    [InlineData("payments.refund.create")]
    [InlineData("production.release.approve")]
    public void LegacyCapabilityIdentifiersRemainAvailable(string capabilityId)
    {
        Assert.Contains(
            new CapabilityLoader().GetCapabilities(),
            item => item.Name == capabilityId);
    }
}
