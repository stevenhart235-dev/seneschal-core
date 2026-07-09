using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class EvaluateApiKeyAuthorizationTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public EvaluateApiKeyAuthorizationTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Evaluate_MissingApiKeyReturns401()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Remove(IntegrationApiKeyAuthorizer.HeaderName);

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeployApplication");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_InvalidApiKeyReturns401()
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Remove(IntegrationApiKeyAuthorizer.HeaderName);
        client.DefaultRequestHeaders.Add(
            IntegrationApiKeyAuthorizer.HeaderName,
            "not-a-real-key");

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeployApplication");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_DisabledApiKeyReturns403()
    {
        using var client = CreateClient(
            IntegrationKey(enabled: false));

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeployApplication");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_IdentityOutsideScopeReturns403()
    {
        using var client = CreateClient(IntegrationKey(
            allowedIdentities: ["Developer"],
            allowedCapabilities: ["DeployApplication"]));

        using var response = await PostEvaluationAsync(
            client,
            "SupportAgent",
            "DeployApplication");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_CapabilityOutsideScopeReturns403()
    {
        using var client = CreateClient(IntegrationKey(
            allowedIdentities: ["Developer"],
            allowedCapabilities: ["DeployApplication"]));

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_ValidScopedKeyAllowsNormalEvaluation()
    {
        using var client = CreateClient(IntegrationKey(
            allowedIdentities: ["Developer"],
            allowedCapabilities: ["DeployApplication"]));

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeployApplication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.NotNull(result);
        Assert.True(result.ContainsKey("decision"));
    }

    [Fact]
    public async Task Evaluate_WildcardIdentityAllowsAnyIdentity()
    {
        using var client = CreateClient(IntegrationKey(
            allowedIdentities: ["*"],
            allowedCapabilities: ["DeployApplication"]));

        using var response = await PostEvaluationAsync(
            client,
            "AnyIntegrationIdentity",
            "DeployApplication");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_WildcardCapabilityAllowsAnyCapability()
    {
        using var client = CreateClient(IntegrationKey(
            allowedIdentities: ["Developer"],
            allowedCapabilities: ["*"]));

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_EnvironmentScopedKeyAllowsMatchingEnvironment()
    {
        using var client = CreateClient(IntegrationKey(environment: "dev"));

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeployApplication",
            environment: "dev");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_EnvironmentScopedKeyDeniesNonMatchingEnvironment()
    {
        using var client = CreateClient(IntegrationKey(environment: "dev"));

        using var response = await PostEvaluationAsync(
            client,
            "Developer",
            "DeployApplication",
            environment: "prod");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_EnvironmentScopedKeyDeniesMissingEnvironment()
    {
        using var client = CreateClient(IntegrationKey(environment: "dev"));

        using var response = await client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "Developer",
                capability = "DeployApplication",
                context = new
                {
                    resource = "api-key-test-resource"
                }
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClient(params IntegrationApiKey[] keys)
    {
        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IntegrationApiKeyLoader>();
                    services.AddSingleton(IntegrationApiKeyLoader.FromKeys(
                        keys.Length == 0
                            ? [IntegrationKey()]
                            : keys));
                });
            })
            .CreateClient();

        client.DefaultRequestHeaders.Remove(IntegrationApiKeyAuthorizer.HeaderName);
        client.DefaultRequestHeaders.Add(
            IntegrationApiKeyAuthorizer.HeaderName,
            "test-scoped-key");

        return client;
    }

    private static IntegrationApiKey IntegrationKey(
        bool enabled = true,
        List<string>? allowedIdentities = null,
        List<string>? allowedCapabilities = null,
        string? environment = null)
    {
        return new IntegrationApiKey
        {
            Name = "test-scoped-key",
            Key = "test-scoped-key",
            Enabled = enabled,
            AllowedIdentities = allowedIdentities ?? ["*"],
            AllowedCapabilities = allowedCapabilities ?? ["*"],
            Environment = environment
        };
    }

    private static async Task<HttpResponseMessage> PostEvaluationAsync(
        HttpClient client,
        string identity,
        string capability,
        string environment = "dev")
    {
        return await client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment,
                    resource = "api-key-test-resource"
                }
            });
    }
}
