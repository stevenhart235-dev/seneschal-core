using Seneschal.Core.Enums;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Core.Tests.Repositories;

public sealed class InMemoryCapabilityCatalogTests
{
    private readonly InMemoryCapabilityCatalog _catalog = new(
    [
        CreateCapability(
            "azure.keyvault.secret.read",
            "platform-security",
            RiskLevel.High),
        CreateCapability(
            "terraform.apply",
            "platform-engineering",
            RiskLevel.Critical),
        CreateCapability(
            "terraform.plan",
            "platform-engineering",
            RiskLevel.Low)
    ]);

    [Fact]
    public async Task SearchAsync_EmptyQueryReturnsCompleteInventory()
    {
        var entries = await _catalog.SearchAsync(new CapabilityCatalogQuery());

        Assert.Equal(3, entries.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCapabilityByIdCaseInsensitively()
    {
        var entry = await _catalog.GetByIdAsync(
            "AZURE.KEYVAULT.SECRET.READ");

        Assert.NotNull(entry);
        Assert.Equal(
            "azure.keyvault.secret.read",
            entry.Capability.Id);
    }

    [Fact]
    public async Task GetByIdAsync_UnknownIdReturnsNull()
    {
        var entry = await _catalog.GetByIdAsync("unknown.capability");

        Assert.Null(entry);
    }

    [Fact]
    public async Task SearchAsync_FiltersByOwnerAndRisk()
    {
        var entries = await _catalog.SearchAsync(
            new CapabilityCatalogQuery
            {
                Owner = "PLATFORM-ENGINEERING",
                RiskLevels = [RiskLevel.Critical]
            });

        var entry = Assert.Single(entries);
        Assert.Equal("terraform.apply", entry.Capability.Id);
    }

    [Fact]
    public void Constructor_RejectsDuplicateIdsCaseInsensitively()
    {
        var capabilities = new[]
        {
            CreateCapability("terraform.apply", "platform", RiskLevel.High),
            CreateCapability("TERRAFORM.APPLY", "platform", RiskLevel.Critical)
        };

        Assert.Throws<ArgumentException>(
            () => new InMemoryCapabilityCatalog(capabilities));
    }

    private static Capability CreateCapability(
        string id,
        string owner,
        RiskLevel riskLevel)
    {
        return new Capability
        {
            Id = id,
            Name = id,
            Provider = id.Split('.')[0],
            Category = "test",
            Description = $"Capability {id}",
            RiskLevel = riskLevel,
            Owner = owner,
            Version = "1.0"
        };
    }
}
