using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class NorthwindIdentityCatalogTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public NorthwindIdentityCatalogTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] LegacyIdentityIds =
    [
        "PlatformEngineer",
        "Developer",
        "FinanceAgent",
        "SupportAgent",
        "platform-agent",
        "deployment-worker",
        "migration-worker",
        "refund-worker",
        "release-approval-worker",
        "github-actions-production",
        "terraform-production"
    ];

    [Fact]
    public void CatalogContainsCompleteUniqueEnterpriseIdentities()
    {
        var identities = new IdentityLoader().GetIdentities();

        Assert.InRange(identities.Count, 20, 30);
        Assert.Equal(
            identities.Count,
            identities.Select(item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Equal(
            identities.Count,
            identities.Select(item => item.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.All(identities, AssertCompleteMetadata);
    }

    [Fact]
    public void CatalogRepresentsExpectedOwnersEnvironmentsTechnologiesAndApplications()
    {
        var identities = new IdentityLoader().GetIdentities();

        AssertContainsAll(
            identities.Select(item => item.Owner),
            "Payments Engineering",
            "Platform Engineering",
            "Release Engineering",
            "Security Engineering",
            "Data Platform",
            "AI Platform",
            "Customer Operations",
            "Finance Systems",
            "Site Reliability Engineering",
            "Developer Experience");
        AssertContainsAll(
            identities.Select(item => item.Environment),
            "Production",
            "Staging",
            "Shared",
            "Corporate",
            "Security");
        AssertContainsAll(
            identities.Select(item => item.Technology),
            "Azure",
            "GitHub",
            "Terraform/OpenTofu",
            "Kubernetes/AKS",
            "OpenAI",
            "PostgreSQL",
            "Slack",
            "Microsoft 365",
            "Custom");
        AssertContainsAll(
            identities.Select(item => item.Application),
            "Payments API",
            "Checkout API",
            "Customer Portal",
            "Payment Processing",
            "Fraud Detection",
            "Notification Platform",
            "API Gateway",
            "Release Pipeline",
            "Infrastructure Platform",
            "Argo CD",
            "Platform Automation",
            "Database Platform",
            "Support Assistant",
            "Finance Assistant",
            "Fraud Analysis",
            "Developer Workstation",
            "Release Governance",
            "Security Operations");

        Assert.Contains(identities, item => item.Environment == "Production");
        Assert.Contains(identities, item => item.Environment != "Production");
    }

    [Theory]
    [MemberData(nameof(LegacyIdentities))]
    public void LegacyIdentityIdentifiersRemainAvailable(string identityId)
    {
        Assert.Contains(
            new IdentityLoader().GetIdentities(),
            item => item.Name == identityId);
    }

    [Theory]
    [InlineData("PlatformEngineer", "Platform Engineer Workstation")]
    [InlineData("Developer", "Developer Laptop")]
    [InlineData("FinanceAgent", "Finance Assistant")]
    [InlineData("platform-agent", "Platform Automation")]
    [InlineData("deployment-worker", "Azure DevOps Pipeline")]
    [InlineData("migration-worker", "Database Migration Worker")]
    [InlineData("refund-worker", "Payment Worker")]
    public void StableLegacyIdentifiersRepresentNorthwindRoles(
        string identityId,
        string expectedDisplayName)
    {
        var identity = new IdentityLoader().GetIdentities()
            .Single(item => item.Name == identityId);
        Assert.Equal(expectedDisplayName, identity.DisplayName);
    }

    [Fact]
    public void DuplicateIdentityIdsAreRejectedCaseInsensitively()
    {
        var result = ConfigurationValidator.Validate(
            [],
            [
                CompleteIdentity("worker"),
                CompleteIdentity("WORKER")
            ],
            [],
            new RuntimeSettings());

        var finding = Assert.Single(
            result.Findings,
            item => item.Category == "IdentityIdentity");
        Assert.Equal("Error", finding.Severity);
        Assert.Equal("worker", finding.RelatedObjectId, ignoreCase: true);
    }

    [Fact]
    public async Task IdentityExplorerRendersRepresentativeNorthwindInventory()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/identity-explorer");

        Assert.Contains("Payments API", html);
        Assert.Contains("payments-api", html);
        Assert.Contains("Argo CD Production", html);
        Assert.Contains("argocd-production", html);
        Assert.Contains("Fraud Analysis Assistant", html);
        Assert.Contains("fraud-analysis-ai", html);
        Assert.Contains("Break-Glass Operator", html);
        Assert.Contains("breakglass-operator", html);
        Assert.Contains(
            "/identity-activity?identityId=payments-api",
            html);
    }

    [Fact]
    public async Task GovernanceGraphCarriesNorthwindIdentityContext()
    {
        using var client = _factory.CreateClient();
        using var document = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync("/graph"));
        var node = document.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() ==
                "identity:payments-api");
        var metadata = node.GetProperty("metadata");

        Assert.Equal("Payments API", node.GetProperty("label").GetString());
        Assert.Equal(
            "Payments Engineering",
            metadata.GetProperty("owner").GetString());
        Assert.Equal(
            "Payments API",
            metadata.GetProperty("application").GetString());
        Assert.Equal(
            "Production",
            metadata.GetProperty("environment").GetString());
        Assert.Equal(
            "Azure",
            metadata.GetProperty("technology").GetString());
        Assert.Contains(
            "Customer payment service",
            metadata.GetProperty("description").GetString());
    }

    public static IEnumerable<object[]> LegacyIdentities() =>
        LegacyIdentityIds.Select(identity => new object[] { identity });

    private static void AssertCompleteMetadata(IdentityDefinition identity)
    {
        Assert.False(string.IsNullOrWhiteSpace(identity.Name));
        Assert.False(string.IsNullOrWhiteSpace(identity.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(identity.Owner));
        Assert.False(string.IsNullOrWhiteSpace(identity.Application));
        Assert.False(string.IsNullOrWhiteSpace(identity.Environment));
        Assert.False(string.IsNullOrWhiteSpace(identity.Technology));
        Assert.False(string.IsNullOrWhiteSpace(identity.Description));
        Assert.False(string.IsNullOrWhiteSpace(identity.Type));
    }

    private static void AssertContainsAll(
        IEnumerable<string?> actual,
        params string[] expected)
    {
        var values = actual
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(expected, value => Assert.Contains(value, values));
    }

    private static IdentityDefinition CompleteIdentity(string name) => new()
    {
        Name = name,
        DisplayName = $"{name} display",
        Owner = "Test Owner",
        Application = "Test Application",
        Environment = "Development",
        Technology = "Custom",
        Description = "Test identity.",
        Type = "Service"
    };
}
