using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class MetricsEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public MetricsEndpointTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Metrics_RendersPrometheusTextFormat()
    {
        using var client = CreateClientWithFreshMetrics();

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "text/plain",
            response.Content.Headers.ContentType?.MediaType);

        var metrics = await response.Content.ReadAsStringAsync();

        Assert.Contains("# HELP seneschal_decisions_total", metrics);
        Assert.Contains("# TYPE seneschal_decisions_total counter", metrics);
        Assert.Contains("seneschal_decisions_total 0", metrics);
        Assert.Contains(
            "# TYPE seneschal_evaluation_duration_ms_avg gauge",
            metrics);
    }

    [Fact]
    public async Task Evaluate_UpdatesPrometheusDecisionMetrics()
    {
        using var client = CreateClientWithFreshMetrics();

        await EvaluateAsync(client, "Developer", "DeployApplication", "dev");
        await EvaluateAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await EvaluateAsync(
            client,
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod");

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metrics = await response.Content.ReadAsStringAsync();

        Assert.Contains("seneschal_decisions_total 3", metrics);
        Assert.Contains("seneschal_decisions_allowed_total 1", metrics);
        Assert.Contains("seneschal_decisions_denied_total 1", metrics);
        Assert.Contains("seneschal_decisions_pending_total 1", metrics);
        Assert.Contains(
            "seneschal_capability_decisions_total{capability=\"DeployApplication\"} 1",
            metrics);
        Assert.Contains(
            "seneschal_identity_decisions_total{identity=\"Developer\"} 2",
            metrics);
        Assert.Contains(
            "seneschal_policy_matches_total{policy=\"Developers cannot delete production databases\"} 1",
            metrics);
        Assert.Contains("seneschal_evaluation_duration_ms_avg", metrics);
    }

    [Fact]
    public async Task Metrics_EscapesPrometheusLabelValues()
    {
        using var client = CreateClientWithFreshMetrics();
        var identity = "metrics\"identity\\test";
        var capability = "metrics\ncapability";

        await EvaluateAsync(client, identity, capability, "dev");

        using var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var metrics = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            "seneschal_identity_decisions_total{identity=\"metrics\\\"identity\\\\test\"} 1",
            metrics);
        Assert.Contains(
            "seneschal_capability_decisions_total{capability=\"metrics\\ncapability\"} 1",
            metrics);
    }

    private HttpClient CreateClientWithFreshMetrics()
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IDecisionMetrics>();
                    services.AddSingleton<IDecisionMetrics, InMemoryDecisionMetrics>();
                });
            })
            .CreateClient();
    }

    private static async Task EvaluateAsync(
        HttpClient client,
        string identity,
        string capability,
        string environment)
    {
        using var response = await client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment,
                    resource = "metrics-test-resource"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
