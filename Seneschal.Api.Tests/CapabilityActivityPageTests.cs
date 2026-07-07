using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityActivityPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public CapabilityActivityPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CapabilityActivity_RendersFriendlyEmptyState()
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

        using var response = await client.GetAsync("/capability-activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capability Activity", html);
        Assert.Contains("No Runtime Activity Yet", html);
        Assert.Contains("Capability activity appears automatically", html);
        Assert.Contains("/evaluate", html);
    }

    [Fact]
    public async Task CapabilityActivity_RendersCapabilityActivityAndDetail()
    {
        var identity = $"CapabilityActivityIdentity-{Guid.NewGuid():N}";
        var capability = $"CapabilityActivity-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "capability-activity-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync(
            $"/capability-activity?capabilityId={Uri.EscapeDataString(capability)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capabilities by Runtime Activity", html);
        Assert.Contains("Total Requests", html);
        Assert.Contains("Allowed", html);
        Assert.Contains("Denied", html);
        Assert.Contains("Pending Approval", html);
        Assert.Contains("Last Used", html);
        Assert.Contains("Avg Duration ms", html);
        Assert.Contains(capability, html);
        Assert.Contains("Allowed Count", html);
        Assert.Contains("Denied Count", html);
        Assert.Contains("Pending Approval Count", html);
        Assert.Contains("Average Evaluation Duration ms", html);
        Assert.Contains("View Audit Events", html);
        Assert.Contains(
            $"/audit?capabilityId={Uri.EscapeDataString(capability)}",
            html);
    }

    [Fact]
    public async Task Dashboard_LinksToCapabilityActivity()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Capability Activity", html);
        Assert.Contains("/capability-activity", html);
    }
}
