using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class IdentityActivityPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public IdentityActivityPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IdentityActivity_RendersFriendlyEmptyState()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/identity-activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Identity Activity", html);
        Assert.Contains("No Runtime Activity Yet", html);
        Assert.Contains("Identity activity appears automatically", html);
        Assert.Contains("/evaluate", html);
    }

    [Fact]
    public async Task IdentityActivity_RendersIdentityActivityAndDetail()
    {
        var identity = $"IdentityActivity-{Guid.NewGuid():N}";
        var capability = $"IdentityCapability-{Guid.NewGuid():N}";

        using (var evaluationResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity,
                capability,
                context = new
                {
                    environment = "dev",
                    resource = "identity-activity-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, evaluationResponse.StatusCode);
        }

        using var response = await _client.GetAsync(
            $"/identity-activity?identityId={Uri.EscapeDataString(identity)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Identities by Runtime Activity", html);
        Assert.Contains("Total Requests", html);
        Assert.Contains("Distinct Capabilities", html);
        Assert.Contains("Denied", html);
        Assert.Contains("Pending Approval", html);
        Assert.Contains("Last Used", html);
        Assert.Contains(identity, html);
        Assert.Contains(capability, html);
        Assert.Contains("Capabilities Used", html);
        Assert.Contains("Recent Evaluations", html);
        Assert.Contains("View Decision Trace", html);
        Assert.Contains($"/capability-activity?capabilityId={Uri.EscapeDataString(capability)}", html);
        Assert.Contains("Open Filtered Audit Trail", html);
        Assert.Contains($"/audit?identityId={Uri.EscapeDataString(identity)}", html);
        Assert.Contains("Back to Identities", html);
        Assert.Contains("href=\"/identity-explorer\">Identities</a> / Identity Activity", html);
        Assert.Contains("View capability profile", html);
        Assert.Contains($"/capability-explorer?capabilityId={Uri.EscapeDataString(capability)}", html);
    }

    [Fact]
    public async Task Dashboard_LinksToIdentityActivity()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Identity Activity", html);
        Assert.Contains("/identity-activity", html);
    }
    [Fact]
    public async Task IdentityActivity_ShowsConfiguredGovernanceWithoutObservedActivity()
    {
        using var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IActivityStore>();
                services.AddSingleton<IActivityStore, InMemoryActivityStore>();
            });
        }).CreateClient();

        var html = await client.GetStringAsync("/identity-activity?identityId=Developer");

        Assert.Contains("Governance Exposure Analysis", html);
        Assert.Contains("current static policy and catalog relationships", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("Decision / environment", html);
        Assert.Contains("Observed Capability Activity", html);
        Assert.Contains("No observed use in selected period", html);
        Assert.Contains("Neither proves authorization or business necessity", html);
        Assert.Contains("Last 30 days", html);
        Assert.Contains("No recent activity", html);
        Assert.Contains("Explainable Exposure Findings", html);
        Assert.Contains("What this does not prove", html);
        Assert.Contains("Findings are not recommendations or scores", html);
        Assert.Contains("Explainable Recommendations", html);
        Assert.Contains("Recommendations are advisory investigation prompts", html);
        Assert.Contains("Source finding", html);
        Assert.Contains("Consider:", html);
        Assert.DoesNotContain(">Fix<", html);
        Assert.DoesNotContain(">Remove<", html);
        Assert.DoesNotContain(">Revoke<", html);
        Assert.DoesNotContain(">Apply<", html);
        Assert.DoesNotContain("View Decision Trace", html);
        Assert.DoesNotContain("Overprivileged", html);
        Assert.DoesNotContain("Safe to remove", html);
        Assert.DoesNotContain("Unauthorized capability", html);
    }

    [Fact]
    public async Task IdentityActivity_DogfoodShapeDistinguishesReviewFromActiveContext()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        var fixture=Path.Combine(root,"Seneschal.Api.Tests","Fixtures","OperatorUx",
            "ContractorDeploymentAgent");
        await using var fixtureFactory=_factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seneschal:Configuration:PoliciesPath",Path.Combine(fixture,"policies.yaml"));
            builder.UseSetting("Seneschal:Configuration:IdentitiesPath",Path.Combine(fixture,"identities.yaml"));
            builder.UseSetting("Seneschal:Configuration:IntegrationKeysPath",Path.Combine(fixture,"integration-keys.yaml"));
            builder.UseSetting("Seneschal:Configuration:CapabilityPacksPath",Path.Combine(root,"capability-packs"));
            builder.ConfigureTestServices(services =>
            {
                var audit=new InMemoryAuditEventStore(
                    completeSinceUtc:DateTimeOffset.UtcNow.AddDays(-5));
                services.RemoveAll<InMemoryAuditEventStore>();
                services.RemoveAll<IAuditEventStore>();
                services.RemoveAll<IAuditSink>();
                services.RemoveAll<IEvaluationCommitCoordinator>();
                services.AddSingleton(audit);
                services.AddSingleton<IAuditEventStore>(audit);
                services.AddSingleton<IAuditSink>(audit);
                services.AddSingleton<IEvaluationCommitCoordinator>(provider =>
                    new InMemoryEvaluationCommitCoordinator(audit,
                        provider.GetRequiredService<InMemoryApprovalStore>()));
                services.RemoveAll<IActivityStore>();
                services.AddSingleton<IActivityStore,InMemoryActivityStore>();
            });
        });
        using var client=fixtureFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Seneschal-Api-Key","operator-ux-contractor-key");

        foreach(var capability in new[]{"github.workflow.dispatch","github.deployment.create",
            "kubernetes.workload.deploy","kubernetes.workload.scale","kubernetes.secret.read"})
        {
            using var response=await client.PostAsJsonAsync("/evaluate",new
            {
                identity="contractor-deployment-agent", capability,
                operationId=$"ux-{capability}",
                context=new{environment="production",resource="contractor-production-delivery"}
            });
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }

        var html=await client.GetStringAsync(
            "/identity-activity?identityId=contractor-deployment-agent");

        Assert.Contains("<strong>6</strong><span>Configured governance capabilities</span>",html);
        Assert.Contains("<strong>5</strong><span>Observed capabilities</span>",html);
        Assert.Contains("capabilityId=kubernetes.secret.modify",html);
        Assert.Equal(1,Count(html,"finding-highriskconfigurednotobserved"));
        Assert.Equal(4,Count(html,"finding-highriskcapabilityactivelyobserved"));
        Assert.Equal(1,Count(html,"recommendation-reviewcurrentgovernancerelationship"));
        Assert.Equal(4,Count(html,"recommendation-reviewactivehighriskgovernancepath"));
        Assert.Equal(2,Count(html,">Review attention<"));
        Assert.Equal(8,Count(html,">Active governance context<"));
        Assert.Contains("attention-review",html);
        Assert.Contains("attention-active",html);
    }

    private static int Count(string value,string fragment) =>
        value.Split(fragment,StringSplitOptions.None).Length-1;
}
