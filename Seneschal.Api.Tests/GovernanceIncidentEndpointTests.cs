using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GovernanceIncidentEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public GovernanceIncidentEndpointTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RepeatedDenials_AggregateIntoOneIncident()
    {
        using var client = CreateIsolatedClient();

        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");

        using var response = await client.GetAsync("/incidents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var incident = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal(
            "DeleteProductionDatabase",
            incident.GetProperty("capabilityId").GetString());
        Assert.Equal("Developer", incident.GetProperty("identityId").GetString());
        Assert.Equal("Critical", incident.GetProperty("severity").GetString());
        Assert.Equal("Open", incident.GetProperty("currentStatus").GetString());
        Assert.Equal(2, incident.GetProperty("occurrenceCount").GetInt32());
        Assert.Equal(
            "Developers cannot delete production databases",
            incident.GetProperty("matchedPolicy").GetString());
    }

    [Fact]
    public async Task OccurrenceCount_IncreasesForRepeatedIncidentKey()
    {
        using var client = CreateIsolatedClient();

        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");

        using var response = await client.GetAsync("/incidents");
        using var document = await ReadJsonAsync(response);
        var incident = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal(3, incident.GetProperty("occurrenceCount").GetInt32());
    }

    [Fact]
    public async Task DifferentCapability_CreatesSeparateIncident()
    {
        using var client = CreateIsolatedClient();
        var unknownCapability = $"incident-capability-{Guid.NewGuid():N}";

        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            unknownCapability,
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            unknownCapability,
            "prod");

        using var response = await client.GetAsync("/incidents");
        using var document = await ReadJsonAsync(response);
        var incidents = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(2, incidents.Count);
        Assert.Contains(incidents, incident =>
            incident.GetProperty("capabilityId").GetString() ==
            "DeleteProductionDatabase");
        Assert.Contains(incidents, incident =>
            incident.GetProperty("capabilityId").GetString() ==
            unknownCapability);
    }

    [Fact]
    public async Task Incidents_ReturnsOrderedIncidents()
    {
        using var client = CreateIsolatedClient();

        await PostEvaluationAsync(
            client,
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod");
        await PostEvaluationAsync(
            client,
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");

        using var response = await client.GetAsync("/incidents");
        using var document = await ReadJsonAsync(response);
        var incidents = document.RootElement.EnumerateArray().ToList();

        Assert.True(incidents.Count >= 2);
        Assert.Equal("Critical", incidents[0].GetProperty("severity").GetString());
        Assert.Equal(
            "DeleteProductionDatabase",
            incidents[0].GetProperty("capabilityId").GetString());
        Assert.Equal("Warning", incidents[1].GetProperty("severity").GetString());
        Assert.Equal(
            "azure.keyvault.secret.read",
            incidents[1].GetProperty("capabilityId").GetString());
    }

    [Fact]
    public async Task OpenIncident_CanBeAcknowledged()
    {
        using var client = CreateIsolatedClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var acknowledgeResponse = await client.PostAsync(
            $"/incidents/{incidentId}/acknowledge",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, acknowledgeResponse.StatusCode);

        var incident = await GetSingleIncidentAsync(client);

        Assert.Equal("Acknowledged", incident.GetProperty("currentStatus").GetString());
    }

    [Fact]
    public async Task OpenIncident_CanBeResolved()
    {
        using var client = CreateIsolatedClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var resolveResponse = await client.PostAsync(
            $"/incidents/{incidentId}/resolve",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, resolveResponse.StatusCode);

        var incident = await GetSingleIncidentAsync(client);

        Assert.Equal("Resolved", incident.GetProperty("currentStatus").GetString());
    }

    [Fact]
    public async Task AcknowledgedIncident_CanBeResolved()
    {
        using var client = CreateIsolatedClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using (var acknowledgeResponse = await client.PostAsync(
            $"/incidents/{incidentId}/acknowledge",
            content: null))
        {
            Assert.Equal(HttpStatusCode.NoContent, acknowledgeResponse.StatusCode);
        }

        using var resolveResponse = await client.PostAsync(
            $"/incidents/{incidentId}/resolve",
            content: null);

        Assert.Equal(HttpStatusCode.NoContent, resolveResponse.StatusCode);

        var incident = await GetSingleIncidentAsync(client);

        Assert.Equal("Resolved", incident.GetProperty("currentStatus").GetString());
    }

    [Fact]
    public async Task MissingIncidentLifecycleAction_Returns404()
    {
        using var client = CreateIsolatedClient();

        using var acknowledgeResponse = await client.PostAsync(
            "/incidents/missing-incident/acknowledge",
            content: null);
        using var resolveResponse = await client.PostAsync(
            "/incidents/missing-incident/resolve",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, acknowledgeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, resolveResponse.StatusCode);
    }

    [Fact]
    public async Task MissingIncidentDetail_Returns404ForApiCaller()
    {
        using var client = CreateIsolatedClient();

        using var response = await client.GetAsync("/incidents/missing-incident");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal("Incident not found", root.GetProperty("title").GetString());
        Assert.Equal(
            "missing-incident",
            root.GetProperty("incidentId").GetString());
    }


    private HttpClient CreateIsolatedClient()
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGovernanceIncidentStore>();
                    services.AddSingleton<IGovernanceIncidentStore>(provider =>
                        new InMemoryGovernanceIncidentStore(
                            provider
                                .GetRequiredService<CapabilityLoader>()
                                .GetCapabilities()
                                .Select(CapabilityMapper.ToCore)));
                });
            })
            .CreateClient();
    }

    private static async Task PostEvaluationAsync(
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
                    resource = "incident-test-resource"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<string> CreateRepeatedDeniedIncidentAsync(
        HttpClient client)
    {
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");
        await PostEvaluationAsync(
            client,
            "Developer",
            "DeleteProductionDatabase",
            "prod");

        var incident = await GetSingleIncidentAsync(client);

        return incident.GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> GetSingleIncidentAsync(
        HttpClient client)
    {
        using var response = await client.GetAsync("/incidents");
        using var document = await ReadJsonAsync(response);
        var incident = Assert.Single(document.RootElement.EnumerateArray());

        return incident.Clone();
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
