using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DashboardDesignLabTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public DashboardDesignLabTests(ApiApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/dashboard1", "Operations", "Recent decisions")]
    [InlineData("/dashboard2", "Investigation workspace", "Decisions requiring investigation")]
    [InlineData("/dashboard3", "Capability posture", "Capability inventory")]
    public async Task PrototypeRoutes_RenderDistinctPrimaryWorkflows(
        string route, string heading, string primarySurface)
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(heading, html);
        Assert.Contains(primarySurface, html);
        Assert.Contains("Design Lab · Static snapshot", html);
        Assert.DoesNotContain(">Live<", html);
        Assert.Contains("@tabler/core@1.4.0", html);
        Assert.Contains("href=\"/dashboard1\"", html);
        Assert.Contains("href=\"/dashboard2\"", html);
        Assert.Contains("href=\"/dashboard3\"", html);
    }

    [Fact]
    public async Task ExistingDashboard_DoesNotExposePrototypeNavigation()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/dashboard");

        Assert.Contains("Technology Posture", html);
        Assert.DoesNotContain("Operational Feed", html);
        Assert.DoesNotContain("Investigation Queue", html);
        Assert.DoesNotContain("href=\"/dashboard1\"", html);
        Assert.DoesNotContain("href=\"/dashboard2\"", html);
        Assert.DoesNotContain("href=\"/dashboard3\"", html);
        Assert.DoesNotContain("Dashboard Design Lab", html);
    }

    [Theory]
    [InlineData("/dashboard1", "No evaluations recorded")]
    [InlineData("/dashboard2", "No denied or pending decisions")]
    [InlineData("/dashboard3", "No capability activity")]
    public async Task PrototypeRoutes_RenderHonestZeroDataStates(
        string route, string expectedState)
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IActivityStore>();
                services.RemoveAll<IAuditEventStore>();
                services.RemoveAll<IAuditSink>();
                services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
                services.AddSingleton<IAuditSink>(provider =>
                    provider.GetRequiredService<IAuditEventStore>());
            });
        }).CreateClient();

        var html = await client.GetStringAsync(route);
        Assert.Contains(expectedState, html);
        Assert.Contains("Static snapshot", html);
        Assert.DoesNotContain(">Live<", html);
    }

    [Fact]
    public async Task InvestigationPrototype_UsesExistingInvestigationRoutes()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/dashboard2");

        Assert.Contains("href=\"/incidents\"", html);
        Assert.Contains("href=\"/approvals\"", html);
        Assert.Contains("href=\"/audit\"", html);
    }

    [Fact]
    public void TablerVersionAndLicenseAttribution_AreRecorded()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var notice = File.ReadAllText(Path.Combine(repositoryRoot, "THIRD_PARTY_NOTICES.md"));

        Assert.Contains("Tabler Core **1.4.0**", notice);
        Assert.Contains("2c73788", notice);
        Assert.Contains("MIT License", notice);
        Assert.Contains("github.com/tabler/tabler/blob/v1.4.0/LICENSE", notice);
    }
}
