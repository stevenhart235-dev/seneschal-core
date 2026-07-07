using System.Net;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DashboardPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public DashboardPageTests(ApiApplicationFactory factory)
    {
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
}
