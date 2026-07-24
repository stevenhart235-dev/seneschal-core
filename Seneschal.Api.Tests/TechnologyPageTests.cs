using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class TechnologyPageTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    public TechnologyPageTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ExplorerAndKnownDetailRoutesRenderWithoutPrimaryNavigationEntry()
    {
        using var client = _factory.CreateClient();
        var index = await client.GetStringAsync("/technologies");
        var detail = await client.GetStringAsync("/technologies/azure");
        var dashboard = await client.GetStringAsync("/dashboard");

        Assert.Contains("Technology Explorer", index);
        Assert.Contains("href=\"/technologies/azure\"", index);
        Assert.Contains("Azure", detail);
        Assert.Contains("Applications", detail);
        Assert.Contains("Capabilities", detail);
        Assert.Contains("Recent decisions", detail);
        Assert.DoesNotContain("href=\"/technologies\"", dashboard);
        Assert.DoesNotContain("href=\"/technologies\"", await client.GetStringAsync("/dashboard1"));
    }

    [Fact]
    public async Task UnknownTechnologyReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/technologies/not-a-technology");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PopulatedDetailUsesExistingInvestigationDestinations()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "SupportAgent", capability = "azure.keyvault.secret.read",
            context = new { environment = "prod", resource = "vault" }
        });
        var html = await client.GetStringAsync("/technologies/azure");

        Assert.Contains("href=\"/identity-activity?identityId=SupportAgent\"", html);
        Assert.Contains("href=\"/capability-activity?capabilityId=azure.keyvault.secret.read\"", html);
        Assert.Contains("href=\"/audit/", html);
        Assert.Contains("href=\"/policies\"", html);
        Assert.Contains("href=\"/audit?matchedPolicy=", html);
        Assert.Contains("Pending Approval", html);
        Assert.DoesNotContain(">Live<", html);
    }

    [Fact]
    public async Task SurfaceUsesLocalIconsAndSemanticCardStates()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "migration-worker", capability = "database.migration.execute",
            context = new { environment = "production", resource = "database" }
        });
        var html = await client.GetStringAsync("/technologies");

        Assert.Contains("technology-attention", html);
        Assert.Contains("technology-state-configured", html);
        Assert.Contains("technology-state-custom", html);
        Assert.Contains("technology-state-unclassified", html);
        Assert.Contains("src=\"/technology-icons/github.svg\"", html);
        Assert.Contains("src=\"/technology-icons/terraform.svg\"", html);
        Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"Open GitHub technology details\"", html);
        Assert.Contains("Investigate capability activity across your technology stack.", html);
        Assert.DoesNotContain("technology-arrow", html);
        Assert.DoesNotContain("aria-hidden=\"true\">→", html);
    }

    [Fact]
    public async Task CatalogOnlyDetailUsesCompactHonestStateBeforeCapabilities()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/technologies/azure");

        Assert.Contains("Configured · not observed", html);
        Assert.Contains("0 applications observed", html);
        Assert.Contains("0 runtime evaluations", html);
        Assert.Contains("8 configured capabilities", html);
        Assert.Contains("8 capabilities", html);
        Assert.DoesNotContain("No applications observed", html);
        Assert.True(html.IndexOf("Configured · not observed", StringComparison.Ordinal) <
            html.IndexOf("<h2>Capabilities</h2>", StringComparison.Ordinal));
        Assert.True(html.IndexOf("<h2>Capabilities</h2>", StringComparison.Ordinal) <
            html.IndexOf("<h2>Recent decisions</h2>", StringComparison.Ordinal));
        Assert.Contains("Azure capabilities and their runtime governance evidence.", html);
    }

    [Fact]
    public async Task PopulatedDetailPrioritizesRecentDecisionsAndUsesConciseCopy()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/evaluate", new
        {
            identity = "deployment-worker", capability = "production.deployment.execute",
            context = new { environment = "production", resource = "service" }
        });

        var html = await client.GetStringAsync("/technologies/github");

        Assert.True(html.IndexOf("<h2>Recent decisions</h2>", StringComparison.Ordinal) <
            html.IndexOf("<h2>Applications</h2>", StringComparison.Ordinal));
        Assert.True(html.IndexOf("<h2>Applications</h2>", StringComparison.Ordinal) <
            html.IndexOf("<h2>Capabilities</h2>", StringComparison.Ordinal));
        Assert.Contains("1 application", html);
        Assert.DoesNotContain("1 applications", html);
        Assert.Contains("6 capabilities", html);
        Assert.Contains("GitHub delivery and automation activity under governance.", html);
        Assert.DoesNotContain("Everything Seneschal knows", html);
        Assert.Contains("href=\"/audit/", html);
        Assert.Contains("href=\"/identity-activity?identityId=deployment-worker\"", html);
        Assert.Contains("href=\"/capability-activity?capabilityId=production.deployment.execute\"", html);
    }

    [Theory]
    [InlineData("terraform", "Terraform and OpenTofu infrastructure activity under governance.")]
    [InlineData("custom", "Customer-specific and internal platform capabilities under governance.")]
    [InlineData("unclassified", "Capabilities without explicit technology metadata.")]
    public async Task DetailUsesApprovedConciseTechnologyDescriptions(string technology, string description)
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync($"/technologies/{technology}");
        Assert.Contains(description, html);
    }

    [Fact]
    public async Task TechnologyIconMappingAndLicenseAreLocalAndRecorded()
    {
        using var client = _factory.CreateClient();
        foreach (var key in new[] { "azure", "github", "terraform", "kubernetes", "postgresql", "aws", "openai", "slack", "m365", "custom", "unclassified" })
        {
            var path = TechnologyIconCatalog.PathFor(key);
            Assert.StartsWith("/technology-icons/", path);
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var notice = File.ReadAllText(Path.Combine(root, "THIRD_PARTY_NOTICES.md"));
        Assert.Contains("Simple Icons 13.21.0", notice);
        Assert.Contains("CC0 1.0 Universal", notice);
        Assert.Contains("Attribution is not required", notice);
    }

    [Fact]
    public async Task EmptyExplorerStateIsHonest()
    {
        using var client = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICapabilityCatalog>();
            services.RemoveAll<IActivityStore>();
            services.RemoveAll<IAuditEventStore>();
            services.RemoveAll<IAuditSink>();
            services.AddSingleton<ICapabilityCatalog>(new InMemoryCapabilityCatalog([]));
            services.AddSingleton<IActivityStore, InMemoryActivityStore>();
            services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
            services.AddSingleton<IAuditSink>(provider => provider.GetRequiredService<IAuditEventStore>());
        })).CreateClient();

        var html = await client.GetStringAsync("/technologies");
        Assert.Contains("No technologies discovered", html);
        Assert.DoesNotContain(">Live<", html);
    }
}
