using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DashboardPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public DashboardPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_RendersInventorySummaryAndQuickLink()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Seneschal Dashboard", html);
        Assert.Contains("Overview", html);
        Assert.Contains("Governance", html);
        Assert.Contains("Operations", html);
        Assert.Contains("Explore", html);
        Assert.Contains("<span>Dashboard</span>", html);
        Assert.Contains("class=\"active\" href=\"/dashboard\"", html);
        Assert.Contains("Monitor", html);
        Assert.Contains("/monitor", html);
        Assert.Contains("/resources", html);
        Assert.Contains("/graph-view", html);
        Assert.Contains("Runtime Activity", html);
        Assert.Contains("Capability Activity", html);
        Assert.Contains("/capability-activity", html);
        Assert.Contains("Identity Activity", html);
        Assert.Contains("/identity-activity", html);
        Assert.Contains("Total Runtime Decisions", html);
        Assert.Contains("Total Capabilities", html);
        Assert.Contains("Total Policies", html);
        Assert.Contains("Total Identities", html);
        Assert.Contains("Total Relationships", html);
        Assert.Contains("Highest Risk Capabilities", html);
        Assert.Contains("Recently Added Capabilities", html);
        Assert.Contains("Open Capability Explorer", html);
        Assert.Contains("/capability-explorer", html);
        Assert.Contains("Seneschal v0.2.1-alpha", html);
    }

    [Fact]
    public async Task Dashboard_RendersFriendlyEmptyStateWhenNoActivityExists()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.RemoveAll<IAuditEventStore>();
                    services.RemoveAll<IAuditSink>();
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                    services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
                    services.AddSingleton<IAuditSink>(
                        services => services.GetRequiredService<IAuditEventStore>());
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No runtime activity has been observed yet.", html);
        Assert.Contains("Activity appears automatically", html);
        Assert.Contains("/evaluate", html);
        Assert.Contains("First-Run Guide", html);
        Assert.Contains("Adoption Checklist", html);
        Assert.Contains("Configure capabilities", html);
        Assert.Contains("Configure identities", html);
        Assert.Contains("Configure policies", html);
        Assert.Contains("Connect an application", html);
        Assert.Contains("Observe runtime activity", html);
        Assert.Contains("Review Monitor dashboard", html);
        Assert.Contains("Enable enforcement when ready", html);
        Assert.Contains("dotnet run --project Seneschal.Api", html);
        Assert.Contains(
            "dotnet run --project Seneschal.Samples.ProtectedApi",
            html);
        Assert.Contains("curl -X POST http://localhost:5000/deploy", html);
        Assert.Contains("Capability Explorer", html);
        Assert.Contains("Policy Explorer", html);
        Assert.Contains("Seneschal.Samples.ProtectedApi/README.md", html);
    }

    [Fact]
    public async Task Dashboard_RendersActivityAfterEvaluation()
    {
        var identity = $"DashboardActivity-{Guid.NewGuid():N}";
        var capability = $"DashboardCapability-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "dashboard-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Total Runtime Decisions", html);
        Assert.Contains("Allowed Decisions", html);
        Assert.Contains("Denied Decisions", html);
        Assert.Contains("Pending Approval Decisions", html);
        Assert.Contains("Most Active Capability", html);
        Assert.Contains("Most Active Identity", html);
        Assert.Contains("Most Matched Policy", html);
        Assert.Contains("Avg Evaluation Duration ms", html);
        Assert.Contains("Top Capabilities", html);
        Assert.Contains("Most Denied Capabilities", html);
        Assert.Contains("Most Active Identities", html);
        Assert.Contains(capability, html);
        Assert.Contains(identity, html);
        Assert.Contains("Audit evidence available", html);
    }

    [Fact]
    public async Task Dashboard_HidesFirstRunGuideAfterRuntimeActivityGrows()
    {
        var identity = $"DashboardFirstRun-{Guid.NewGuid():N}";

        for (var index = 0; index < 3; index++)
        {
            using var evaluationResponse = await _client.PostAsJsonAsync(
                "/evaluate",
                new
                {
                    identity,
                    capability = $"DashboardFirstRunCapability-{index}",
                    context = new
                    {
                        environment = "dev",
                        resource = $"dashboard-first-run-resource-{index}"
                    }
                });

            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("First-Run Guide", html);
        Assert.Contains("Runtime Activity", html);
        Assert.Contains("Top Capabilities", html);
    }

    [Fact]
    public async Task Resources_RendersFromNavigationRoute()
    {
        using var response = await _client.GetAsync("/resources");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Governance / Resources", html);
        Assert.Contains("Resource Explorer Coming Soon", html);
        Assert.Contains("class=\"active\" href=\"/resources\"", html);
    }
}
