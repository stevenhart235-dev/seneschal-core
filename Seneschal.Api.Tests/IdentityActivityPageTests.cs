using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class IdentityActivityPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public IdentityActivityPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IdentityActivity_RendersFriendlyEmptyState()
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

        using var response = await client.GetAsync("/identity-activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Identity Activity", html);
        Assert.Contains("No Runtime Activity Yet", html);
        Assert.Contains("Identity activity appears automatically", html);
        Assert.Contains("/evaluate", html);
    }

    [Fact]
    public async Task IdentityActivity_RendersIdentityActivityAndDetail()
    {
        var identity = $"IdentityActivity-{Guid.NewGuid():N}";
        var capability = $"IdentityCapability-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "identity-activity-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync(
            $"/identity-activity?identityId={Uri.EscapeDataString(identity)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Identities by Runtime Activity", html);
        Assert.Contains("Total Requests", html);
        Assert.Contains("Distinct Capabilities", html);
        Assert.Contains("Denied", html);
        Assert.Contains("Pending Approval", html);
        Assert.Contains("Last Used", html);
        Assert.Contains(identity, html);
        Assert.Contains(capability, html);
        Assert.Contains("Capabilities Used", html);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains($"/audit?identityId={Uri.EscapeDataString(identity)}", html);
    }

    [Fact]
    public async Task Dashboard_LinksToIdentityActivity()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Identity Activity", html);
        Assert.Contains("/identity-activity", html);
    }
}
