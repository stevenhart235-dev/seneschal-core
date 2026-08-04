using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Persistence.PostgreSql;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class HealthDiagnosticsEndpointTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthDiagnosticsEndpointTests(ApiApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthyJson()
    {
        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Live_ReturnsLiveJson()
    {
        using var response = await _client.GetAsync("/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("live", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Ready_ReturnsReadinessDetails()
    {
        using var response = await _client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("ready", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("capabilitiesLoaded").GetBoolean());
        Assert.True(root.GetProperty("identitiesLoaded").GetBoolean());
        Assert.True(root.GetProperty("policiesLoaded").GetBoolean());
        Assert.True(root.GetProperty("runtimeSettingsLoaded").GetBoolean());
        Assert.Equal("InMemory",
            root.GetProperty("persistenceProvider").GetString());
        Assert.True(root.GetProperty("persistenceReachable").GetBoolean());
        Assert.True(root.GetProperty("migrationsCurrent").GetBoolean());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Ready_ReturnsSafe503WhenSelectedPersistenceIsUnavailable()
    {
        using var factory = new UnavailableReadinessFactory();
        using var client = factory.CreateClient();

        using var readyResponse = await client.GetAsync("/ready");
        using var healthResponse = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        var body = await readyResponse.Content.ReadAsStringAsync();
        Assert.Contains("not_ready", body);
        Assert.Contains("PostgreSql", body);
        Assert.DoesNotContain("Password", body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("57P01", body,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExhaustedPostgreSqlRead_ReturnsSafe503()
    {
        using var factory = new UnavailableReadFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("temporarily unavailable", body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provider detail", body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PostgreSQL", body,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnostics_ReturnsCountsAndComponentTypes()
    {
        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = $"Diagnostics-{Guid.NewGuid():N}",
                capability = $"DiagnosticsCapability-{Guid.NewGuid():N}",
                context = new
                {
                    environment = "dev",
                    resource = "diagnostics-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = await ReadJsonAsync(response);
        var root = document.RootElement;

        Assert.Equal("LogOnly", root.GetProperty("currentRuntimeMode").GetString());
        Assert.True(root.GetProperty("capabilityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("identityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("policyCount").GetInt32() > 0);
        Assert.True(root.GetProperty("auditEventCount").GetInt32() > 0);
        Assert.True(root.GetProperty("activityCapabilityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("activityIdentityCount").GetInt32() > 0);
        Assert.True(root.GetProperty("activityPolicyCount").GetInt32() > 0);
        Assert.Equal("NullDecisionExporter", root.GetProperty("exporterType").GetString());
        Assert.Equal("InMemoryDecisionMetrics", root.GetProperty("metricsType").GetString());
        Assert.True(root.TryGetProperty("timestampUtc", out var timestamp));
        Assert.Equal(JsonValueKind.String, timestamp.ValueKind);
    }

    [Fact]
    public async Task Diagnostics_DoesNotExposeRawPolicyContentsOrSecrets()
    {
        using var response = await _client.GetAsync("/diagnostics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(
            "Developer is allowed to deploy applications to dev",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Reading production secrets requires approval",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Developers can deploy to dev",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Support secret reads require approval",
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class UnavailableReadinessFactory : ApiApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPersistenceReadiness>();
                services.AddSingleton<IPersistenceReadiness>(
                    new UnavailableReadiness());
            });
        }
    }

    private sealed class UnavailableReadiness : IPersistenceReadiness
    {
        public Task<PersistenceReadinessResult> CheckAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PersistenceReadinessResult(
                "PostgreSql", Reachable: false, MigrationsCurrent: false));
    }

    private sealed class UnavailableReadFactory : ApiApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditEventStore>();
                services.AddSingleton<IAuditEventStore>(
                    new UnavailableAuditStore());
            });
        }
    }

    private sealed class UnavailableAuditStore : IAuditEventStore
    {
        public Task WriteAsync(AuditEvent auditEvent,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AuditEvent?> GetByIdAsync(string id,
            CancellationToken cancellationToken = default) =>
            Task.FromException<AuditEvent?>(Unavailable());

        public Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
            int count = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyCollection<AuditEvent>>(Unavailable());

        private static PostgreSqlReadUnavailableException Unavailable() =>
            new(new IOException("provider detail"));
    }
}
