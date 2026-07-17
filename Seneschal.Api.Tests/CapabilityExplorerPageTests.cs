using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Enums;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class CapabilityExplorerPageTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public CapabilityExplorerPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task KnownCapabilityRendersOperationalIdentityAndGovernanceSummary()
    {
        var html = await Get("/capability-explorer?capabilityId=DeployApplication");

        Assert.Contains("Capability profile", html);
        Assert.Contains("Deploy Application", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Platform Engineering", html);
        Assert.Contains("Medium risk", html);
        Assert.Contains("mode-logonly", html);
        Assert.Contains("Governance summary", html);
        Assert.Contains("owned by Platform Engineering", html);
        Assert.Contains("currently operating in LogOnly mode", html);
        Assert.Contains("Open documentation", html);
        Assert.Contains("Open Interactive Graph", html);
        Assert.Contains("aria-current=\"page\"><span>Capabilities", html);
    }

    [Fact]
    public async Task RelationshipGroupsAndImpactUseExistingRelationships()
    {
        var html = await Get("/capability-explorer?capabilityId=DeployApplication");

        Assert.Contains("Governance relationships", html);
        Assert.Contains(">Identities<", html);
        Assert.Contains(">Policies<", html);
        Assert.Contains(">Resources<", html);
        Assert.Contains("Governance windows", html);
        Assert.Contains("Developer", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("Relationship impact", html);
        Assert.Contains("Changes to this capability may affect", html);
        Assert.Contains("/identity-activity?identityId=Developer", html);
        Assert.Contains("/policies?policyId=Developers%20can%20deploy%20to%20dev", html);
    }

    [Fact]
    public async Task EmptyRuntimeAndRelationshipStatesAreIntentional()
    {
        using var client = CreateClientWithRuntime();
        var html = await Get(client,
            "/capability-explorer?capabilityId=payments.refund.create");

        Assert.Contains("Evaluations</span><strong>0", html);
        Assert.Contains("No recent activity", html);
        Assert.Contains("No active governance window applies", html);
        Assert.True(
            html.Contains("No related resources", StringComparison.Ordinal) ||
            html.Contains("Related resources", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecentActivityRendersAllOperationalFieldsAndTraceLink()
    {
        var auditEvent = new AuditEvent
        {
            Id = "trace-123",
            RequestId = "request-123",
            TimestampUtc = DateTimeOffset.UtcNow,
            IdentityId = "Developer",
            CapabilityId = "DeployApplication",
            Environment = "dev",
            ResourceId = "checkout-api",
            Decision = DecisionType.Deny,
            PolicyDecision = DecisionType.Deny,
            EnforcementMode = EnforcementMode.Enforce,
            Reason = "Denied for test",
            ApprovalOperationId = "deployment-007",
            EvaluationDurationMs = 7,
            MatchedPolicies = ["Developers can deploy to dev"]
        };
        using var client = CreateClientWithRuntime([auditEvent]);
        var html = await Get(client,
            "/capability-explorer?capabilityId=DeployApplication");

        Assert.Contains("Recent activity", html);
        Assert.Contains("Developer", html);
        Assert.Contains("checkout-api", html);
        Assert.Contains("dev", html);
        Assert.Contains("Deny", html);
        Assert.Contains("Enforce", html);
        Assert.Contains("deployment-007", html);
        Assert.Contains("href=\"/audit/trace-123\"", html);
        Assert.Contains("Evaluations</span><strong>1", html);
        Assert.Contains("Denies</span><strong class=\"metric-deny\">1", html);
        Assert.Contains("Recent activity includes denied decisions", html);
    }

    [Fact]
    public async Task ActiveGovernanceWindowAppearsOnlyWhenItApplies()
    {
        var window = new InMemoryGovernanceWindowStore();
        window.SetState(true, GovernanceWindowMode.Enforce);
        using var client = CreateClientWithRuntime(window: window);
        var html = await Get(client,
            "/capability-explorer?capabilityId=production.deployment.execute");

        Assert.Contains("Production Freeze", html);
        Assert.Contains("The active Production Freeze window participates", html);
        Assert.Contains("Weekend production freeze", html);
        Assert.Contains("href=\"/governance-windows\"", html);
    }

    [Fact]
    public async Task NoSelectionPromptsForCatalogSearch()
    {
        var html = await Get("/capability-explorer");
        Assert.Contains("No capability selected", html);
        Assert.Contains("Search the configured catalog", html);
    }

    [Fact]
    public async Task SearchAndSelectionNavigationRemainAvailable()
    {
        var html = await Get("/capability-explorer?q=secret");
        Assert.Contains("Search results", html);
        Assert.Contains("azure.keyvault.secret.read", html);
        Assert.Contains("Read a secret from an Azure Key Vault", html);
        Assert.Contains("capabilityId=azure.keyvault.secret.read", html);
    }

    [Theory]
    [InlineData("owner=Release%20Engineering", "Execute Production Deployment")]
    [InlineData("risk=Critical", "Apply Production Infrastructure")]
    [InlineData("category=Payments", "Create Payment Refund")]
    [InlineData("lifecycle=Preview", "Approve Production Release")]
    public async Task CatalogFiltersRemainAvailable(string filter, string expected)
    {
        Assert.Contains(expected, await Get($"/capability-explorer?{filter}"));
    }

    [Fact]
    public async Task SearchAndUnknownCapabilityEmptyStatesRender()
    {
        var miss = await Get("/capability-explorer?q=missing-capability");
        Assert.Contains("No capabilities matched", miss);
        Assert.Contains("configured capability catalog", miss);

        var unknown = await Get(
            "/capability-explorer?capabilityId=unknown-capability");
        Assert.Contains("Capability not found", unknown);
        Assert.Contains("profiles are created from catalog entries", unknown);
    }

    private HttpClient CreateClientWithRuntime(
        IReadOnlyCollection<AuditEvent>? events = null,
        InMemoryGovernanceWindowStore? window = null)
    {
        var audit = new InMemoryAuditEventStore();
        var activity = new InMemoryActivityStore();
        foreach (var item in events ?? [])
        {
            audit.WriteAsync(item).GetAwaiter().GetResult();
            activity.RecordAsync(item).GetAwaiter().GetResult();
        }
        return _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuditEventStore>();
            services.RemoveAll<IAuditSink>();
            services.RemoveAll<IActivityStore>();
            services.RemoveAll<IGovernanceWindowStore>();
            services.AddSingleton<IAuditEventStore>(audit);
            services.AddSingleton<IAuditSink>(audit);
            services.AddSingleton<IActivityStore>(activity);
            services.AddSingleton<IGovernanceWindowStore>(
                window ?? new InMemoryGovernanceWindowStore());
        })).CreateClient();
    }

    private async Task<string> Get(string path) => await Get(_client, path);
    private static async Task<string> Get(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
}
