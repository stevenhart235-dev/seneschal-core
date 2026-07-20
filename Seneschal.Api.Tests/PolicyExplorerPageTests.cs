using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PolicyExplorerPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public PolicyExplorerPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Policies_WithHtmlAcceptHeaderRendersPolicyExplorer()
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
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/policies");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Policy Explorer</title>", html);
        Assert.Contains("<h1>Policy Explorer</h1>", html);
        Assert.Contains("class=\"active\" href=\"/policies\"", html);
        Assert.Contains("Policy Profile", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("decision-badge decision-allow", html);
        Assert.Contains("Runtime mode: LogOnly", html);
        Assert.Contains("Priority: 4", html);
        Assert.Contains("Applies To", html);
        Assert.Contains("Related identities", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Related capabilities", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Related resources", html);
        Assert.Contains("environment:dev", html);
        Assert.Contains("Runtime Summary", html);
        Assert.Contains("Match count", html);
        Assert.Contains("Last matched", html);
        Assert.Contains("Related denied", html);
        Assert.Contains("Related pending approval", html);
        Assert.Contains("Conditions", html);
        Assert.Contains("<dt>Identity</dt>", html);
        Assert.Contains("<dd>Developer</dd>", html);
        Assert.Contains("<h3>Audit Trail</h3>", html);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains("/audit?matchedPolicy=Developers%20can%20deploy%20to%20dev", html);
        Assert.Contains("Recommendations", html);
        Assert.Contains("Policy has not been exercised yet", html);
        Assert.Contains("Seneschal v0.2.1-alpha", html);
    }

    [Fact]
    public async Task Policies_RenderRuntimeSummaryAndRelatedAudit()
    {
        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "Developer",
                capability = "DeployApplication",
                context = new
                {
                    environment = "dev",
                    resource = "policy-profile-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/policies");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("Match count", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Allow", html);
        Assert.Contains(
            "Developer is allowed to deploy applications to dev",
            html);
        Assert.Contains("Policy appears active and stable", html);
    }
}
