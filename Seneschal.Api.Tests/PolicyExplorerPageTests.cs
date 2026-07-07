using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PolicyExplorerPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public PolicyExplorerPageTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Policies_WithHtmlAcceptHeaderRendersPolicyExplorer()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/policies");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/html",
            response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Seneschal Policy Explorer", html);
        Assert.Contains("class=\"active\" href=\"/policies\"", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("decision-badge decision-allow", html);
        Assert.Contains("Priority: 4", html);
        Assert.Contains("Related identities", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Related capabilities", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Related resources", html);
        Assert.Contains("environment:dev", html);
        Assert.Contains("Seneschal v0.2.1-alpha", html);
    }
}
