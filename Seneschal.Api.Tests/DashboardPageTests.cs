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
        Assert.Contains("class=\"active\" href=\"/dashboard\"", html);
        Assert.Contains("Monitor", html);
        Assert.Contains("/monitor", html);
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
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("No runtime activity has been observed yet.", html);
        Assert.Contains("Activity appears automatically", html);
        Assert.Contains("/evaluate", html);
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
    }
}
