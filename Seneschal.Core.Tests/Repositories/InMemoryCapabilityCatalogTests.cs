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
    public async Task SearchAsync_FiltersByCategoryAndLifecycle()
    {
        var entries = await _catalog.SearchAsync(
            new CapabilityCatalogQuery
            {
                Category = "INFRASTRUCTURE",
                Lifecycle = "ACTIVE"
            });

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry =>
        {
            Assert.Equal("infrastructure", entry.Capability.Category);
            Assert.Equal("Active", entry.Capability.Lifecycle);
        });
    }

    [Theory]
    [InlineData("KEYVAULT", "azure.keyvault.secret.read")]
    [InlineData("Terraform Apply", "terraform.apply")]
    [InlineData("execution plan", "terraform.plan")]
    [InlineData("deployment", "terraform.apply")]
    public async Task SearchAsync_FiltersByPartialTextCaseInsensitively(
        string searchText,
        string expectedCapabilityId)
    {
        var entries = await _catalog.SearchAsync(
            new CapabilityCatalogQuery
            {
                SearchText = searchText
            });

        var entry = Assert.Single(entries);
        Assert.Equal(expectedCapabilityId, entry.Capability.Id);
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
        var name = id switch
        {
            "terraform.apply" => "Terraform Apply",
            "terraform.plan" => "Terraform Plan",
            _ => id
        };
        var description = id switch
        {
            "terraform.plan" => "Create an infrastructure execution plan.",
            _ => $"Capability {id}"
        };
        var tags = id switch
        {
            "terraform.apply" => new List<string> { "deployment" },
            _ => new List<string>()
        };

        return new Capability
        {
            Id = id,
            Name = name,
            Provider = id.Split('.')[0],
            Category = id.StartsWith("terraform", StringComparison.Ordinal)
                ? "infrastructure"
                : "security",
            Description = description,
            RiskLevel = riskLevel,
            Owner = owner,
            Lifecycle = "Active",
            Version = "1.0",
            Tags = tags
        };
    }
}
