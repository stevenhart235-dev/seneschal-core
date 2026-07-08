using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class DecisionExportEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public DecisionExportEndpointTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Exports_DefaultExporterReturnsEmptyCollection()
    {
        using var response = await _client.GetAsync("/exports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [Fact]
    public async Task Exports_ReturnsDecisionEventsFromInMemoryExporter()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDecisionExporter>();
                    services.AddSingleton<IDecisionExporter, InMemoryDecisionExporter>();
                });
            })
            .CreateClient();

        using (var evaluationResponse = await client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "Developer",
                capability = "DeployApplication",
                context = new
                {
                    environment = "dev",
                    resource = "export-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await client.GetAsync("/exports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var export = Assert.Single(document.RootElement.EnumerateArray());

        Assert.True(export.TryGetProperty("timestamp", out _));
        Assert.Equal("Developer", export.GetProperty("identity").GetString());
        Assert.Equal("DeployApplication", export.GetProperty("capability").GetString());
        Assert.Equal("dev", export.GetProperty("environment").GetString());
        Assert.Equal("Allow", export.GetProperty("decision").GetString());
        Assert.Equal(
            "Developers can deploy to dev",
            export.GetProperty("matchedPolicy").GetString());
        Assert.True(export.GetProperty("evaluationDurationMs").GetInt32() >= 0);
        Assert.Equal(
            "Developer is allowed to deploy applications to dev",
            export.GetProperty("reason").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
