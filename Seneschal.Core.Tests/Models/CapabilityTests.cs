using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Xunit;

namespace Seneschal.Core.Tests.Models;

public sealed class CapabilityTests
{
    [Fact]
    public void Capability_RepresentsCatalogMetadata()
    {
        var capability = new Capability
        {
            Id = "azure.keyvault.secret.read",
            Name = "Read Key Vault secret",
            Provider = "azure",
            Category = "secret-management",
            Description = "Read a secret value from Azure Key Vault.",
            RiskLevel = RiskLevel.High,
            Owner = "platform-security",
            Version = "1.0",
            Tags = ["secrets", "production"]
        };

        Assert.Equal("azure.keyvault.secret.read", capability.Id);
        Assert.Equal("Read Key Vault secret", capability.Name);
        Assert.Equal("azure", capability.Provider);
        Assert.Equal("secret-management", capability.Category);
        Assert.Equal("Read a secret value from Azure Key Vault.", capability.Description);
        Assert.Equal(RiskLevel.High, capability.RiskLevel);
        Assert.Equal("platform-security", capability.Owner);
        Assert.Equal("1.0", capability.Version);
        Assert.Equal(["secrets", "production"], capability.Tags);
    }

    [Fact]
    public void Risk_AndRiskLevel_AreCompatible()
    {
        var legacyCapability = new Capability
        {
            Id = "terraform.apply",
            Provider = "terraform",
            Category = "infrastructure",
            Description = "Apply an infrastructure plan.",
            Risk = RiskLevel.Critical
        };

        Assert.Equal(RiskLevel.Critical, legacyCapability.RiskLevel);
        Assert.Equal(RiskLevel.Critical, legacyCapability.Risk);
    }
}
