using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Models;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Enums;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class PreflightEndpointTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public PreflightEndpointTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ValidRequest_ReturnsEvaluationWithoutCommittingState()
    {
        await using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();
        var approvals = factory.Services.GetRequiredService<IApprovalStore>();
        var audit = factory.Services.GetRequiredService<IAuditEventStore>();
        var incidents = factory.Services.GetRequiredService<IGovernanceIncidentStore>();
        var beforeAudit = (await audit.GetRecentAsync()).Count;
        var beforeIncidents = (await incidents.GetAllAsync()).Count;

        using var response = await PostAsync(
            client,
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod",
            "preflight-approval-resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<
            Seneschal.Api.Models.DecisionResult>();
        Assert.Equal("requires_approval", result?.Decision);
        Assert.Equal("ContinueLogOnly", result?.ExecutionGuidance);
        Assert.Empty(approvals.GetAll());
        Assert.Equal(beforeAudit, (await audit.GetRecentAsync()).Count);
        Assert.Equal(beforeIncidents, (await incidents.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ApprovedRecord_IsPreviewedWithoutBeingConsumed()
    {
        await using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();
        var approvals = factory.Services.GetRequiredService<IApprovalStore>();
        var record = approvals.GetOrCreate(
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod",
            "approved-preflight-resource",
            "preflight test",
            DateTimeOffset.UtcNow).Record;
        approvals.Resolve(
            record.Id,
            ApprovalStatus.Approved,
            "reviewer",
            DateTimeOffset.UtcNow);

        using var response = await PostAsync(
            client,
            "SupportAgent",
            "azure.keyvault.secret.read",
            "prod",
            "approved-preflight-resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<
            Seneschal.Api.Models.DecisionResult>();
        Assert.Equal("allow", result?.Decision);
        Assert.Equal(ApprovalStatus.Approved, approvals.GetById(record.Id)?.Status);
        Assert.Null(approvals.GetById(record.Id)?.ConsumedAt);
    }

    [Fact]
    public async Task InvalidApiKey_ReturnsAuthenticationCategory()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Remove(IntegrationApiKeyAuthorizer.HeaderName);
        client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthorizer.HeaderName, "invalid");

        using var response = await PostAsync(client, "Developer", "DeployApplication");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "authentication_failure");
    }

    [Theory]
    [InlineData("NotConfigured", "DeployApplication", "invalid_identity")]
    [InlineData("Developer", "not.configured", "invalid_capability")]
    public async Task InvalidCatalogValue_ReturnsSpecificCategory(
        string identity,
        string capability,
        string expectedCode)
    {
        using var client = _factory.CreateClient();
        using var response = await PostAsync(client, identity, capability);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, expectedCode);
    }

    [Fact]
    public async Task OutOfScopeRequest_ReturnsScopeMismatch()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IntegrationApiKeyLoader>();
                services.AddSingleton(IntegrationApiKeyLoader.FromKeys(
                [
                    new IntegrationApiKey
                    {
                        Name = "limited",
                        Key = "limited-key",
                        Enabled = true,
                        AllowedIdentities = ["Developer"],
                        AllowedCapabilities = ["DeployApplication"],
                        Environment = "dev"
                    }
                ]));
            });
        }).CreateClient();
        client.DefaultRequestHeaders.Remove(IntegrationApiKeyAuthorizer.HeaderName);
        client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthorizer.HeaderName, "limited-key");

        using var response = await PostAsync(
            client,
            "Developer",
            "DeployApplication",
            environment: "prod");

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "scope_mismatch");
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string identity,
        string capability,
        string environment = "dev",
        string resource = "preflight-test") =>
        client.PostAsJsonAsync("/preflight", new
        {
            identity,
            capability,
            context = new { environment, resource }
        });

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal(expectedCode, body?["code"]?.ToString());
    }
}
