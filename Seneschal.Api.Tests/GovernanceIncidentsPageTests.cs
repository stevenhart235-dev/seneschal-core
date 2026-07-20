using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Mappers;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class GovernanceIncidentsPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public GovernanceIncidentsPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IncidentsPage_Renders()
    {
        using var client = CreateHtmlClient();

        using var response = await client.GetAsync("/incidents");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Governance Incidents", html);
        Assert.Contains("Operations / Incidents", html);
        Assert.Contains("<span>Incidents</span>", html);
    }

    [Fact]
    public async Task IncidentsPage_EmptyStateRenders()
    {
        using var client = CreateHtmlClient();

        using var response = await client.GetAsync("/incidents");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("No governance incidents detected.", html);
        Assert.Contains("Open Incidents", html);
        Assert.Contains("Total Occurrences", html);
    }

    [Fact]
    public async Task IncidentsPage_RepeatedDenialsAppearWithSummaryAndAuditLink()
    {
        using var client = CreateHtmlClient();

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
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Incident Queue", html);
        Assert.Contains("Repeated denied governance decision", html);
        Assert.Contains("DeleteProductionDatabase", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Developers cannot delete production databases", html);
        Assert.Contains("Critical", html);
        Assert.Contains("Open", html);
        Assert.Contains("Acknowledge", html);
        Assert.Contains("Resolve", html);
        Assert.Contains(">2</", html);
        Assert.Contains(
            "/audit?capabilityId=DeleteProductionDatabase&amp;identityId=Developer",
            html);
    }

    [Fact]
    public async Task IncidentDetailPage_RendersForExistingIncident()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/incidents\">Incidents</a> / Incident detail", html);
        Assert.Contains("Repeated denied governance decision", html);
        Assert.Contains("Critical", html);
        Assert.Contains("Open", html);
        Assert.Contains("DeleteProductionDatabase", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Developers cannot delete production databases", html);
        Assert.Contains("Occurrence Count", html);
        Assert.Contains(">2</", html);
    }

    [Fact]
    public async Task IncidentDetailPage_MissingIncidentReturnsFriendly404()
    {
        using var client = CreateHtmlClient();

        using var response = await client.GetAsync("/incidents/missing-incident");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Incident not found", html);
        Assert.Contains("missing-incident", html);
        Assert.Contains("Back to Incidents", html);
    }

    [Fact]
    public async Task IncidentDetailPage_ShowsRelatedAuditLink()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains(
            "/audit?capabilityId=DeleteProductionDatabase&amp;identityId=Developer",
            html);
        Assert.Contains("Developers cannot delete production databases", html);
    }

    [Fact]
    public async Task IncidentDetailPage_TimelineRendersAuditHistory()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Incident Timeline", html);
        Assert.Contains("First Seen", html);
        Assert.Contains("Last Seen", html);
        Assert.Contains("Total Occurrences", html);
        Assert.Contains("Matched Policy", html);
        Assert.Contains("Decision Reason", html);
        Assert.Contains("Developers cannot delete production databases", html);
        Assert.Contains("Deny", html);
    }

    [Fact]
    public async Task IncidentDetailPage_InvestigationLinksRender()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Investigation", html);
        Assert.Contains("Investigate Capability Activity", html);
        Assert.Contains(
            "/capability-activity?capabilityId=DeleteProductionDatabase&amp;identity=Developer",
            html);
        Assert.Contains("Related Identity", html);
        Assert.Contains("/identity-activity?identityId=Developer", html);
        Assert.Contains("Related Policy", html);
        Assert.Contains("/policies", html);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains(
            "/audit?capabilityId=DeleteProductionDatabase&amp;identityId=Developer",
            html);
        Assert.Equal(1, html.Split("Open Filtered Audit Trail").Length - 1);
    }

    [Fact]
    public async Task IncidentDetailPage_RecommendationRenders()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Recommendation", html);
        Assert.Contains(
            "Repeated denied access detected. Review policy and caller.",
            html);
    }

    [Fact]
    public async Task IncidentDetailPage_SummaryStripRenders()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("incident-summary-strip", html);
        Assert.Contains("Severity", html);
        Assert.Contains("Status", html);
        Assert.Contains("Occurrence Count", html);
        Assert.Contains("Age", html);
        Assert.Contains("Last Seen", html);
    }

    [Fact]
    public async Task IncidentDetailPage_RendersCorrectLifecycleActions()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using (var openResponse = await client.GetAsync($"/incidents/{incidentId}"))
        {
            var openHtml = await openResponse.Content.ReadAsStringAsync();

            Assert.Contains($"/incidents/{incidentId}/acknowledge", openHtml);
            Assert.Contains($"/incidents/{incidentId}/resolve", openHtml);
        }

        using (var acknowledgeResponse = await client.PostAsync(
            $"/incidents/{incidentId}/acknowledge",
            content: null))
        {
            Assert.Equal(HttpStatusCode.OK, acknowledgeResponse.StatusCode);
        }

        using var acknowledgedResponse = await client.GetAsync(
            $"/incidents/{incidentId}");
        var acknowledgedHtml = await acknowledgedResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            $"/incidents/{incidentId}/acknowledge",
            acknowledgedHtml);
        Assert.Contains($"/incidents/{incidentId}/resolve", acknowledgedHtml);
        Assert.Contains("Acknowledged", acknowledgedHtml);
    }

    [Fact]
    public async Task IncidentsPage_LinksRowsToDetailPage()
    {
        using var client = CreateHtmlClient();
        var incidentId = await CreateRepeatedDeniedIncidentAsync(client);

        using var response = await client.GetAsync("/incidents");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"/incidents/{incidentId}", html);
    }

    [Fact]
    public async Task IncidentsPage_RendersAppropriateActionButtons()
    {
        using var client = CreateHtmlClient();

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

        var incidentId = await GetSingleIncidentIdAsync(client);

        using (var openResponse = await client.GetAsync("/incidents"))
        {
            var openHtml = await openResponse.Content.ReadAsStringAsync();

            Assert.Contains("Acknowledge", openHtml);
            Assert.Contains("/acknowledge", openHtml);
            Assert.Contains("Resolve", openHtml);
        }

        using (var acknowledgeResponse = await client.PostAsync(
            $"/incidents/{incidentId}/acknowledge",
            content: null))
        {
            Assert.Equal(HttpStatusCode.OK, acknowledgeResponse.StatusCode);
        }

        using var acknowledgedResponse = await client.GetAsync("/incidents");
        var acknowledgedHtml = await acknowledgedResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain("/acknowledge", acknowledgedHtml);
        Assert.Contains("Resolve", acknowledgedHtml);
        Assert.Contains("Acknowledged", acknowledgedHtml);
    }

    private HttpClient CreateHtmlClient()
    {
        var client = _factory
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

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));

        return client;
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
                    resource = "incident-page-test-resource"
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

        return await GetSingleIncidentIdAsync(client);
    }

    private static async Task<string> GetSingleIncidentIdAsync(
        HttpClient client)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/incidents");
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request);
        using var document = await System.Text.Json.JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var incident = Assert.Single(document.RootElement.EnumerateArray());

        return incident.GetProperty("id").GetString()!;
    }
}
